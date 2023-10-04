using System;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using WaterSystem.Settings;
using Graphics = UnityEngine.Graphics;

namespace WaterSystem.Rendering
{
    public class MeshSurface
    {
        private Vector4 settingsShader = Vector4.one;

        private NativeList<WaterTile> baseTilesA;
        private NativeList<WaterTile> baseTilesB;
        private NativeList<WaterTile> WaterTiles;
        private NativeArray<float4> FrustumPlanes;

        private NativeArray<int> TileCount;
        private int iterations = 5;

        private NativeArray<float4x4> TileMatrices;
        private static Matrix4x4[] TileTempMatrices = new Matrix4x4[512];

        public Matrix4x4 transformMatrix;

        private Plane[] cullingPlanes;

        public MeshSurface()
        {
            CreateArrays();
        }

        private void CreateArrays()
        {
            FrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent);
            baseTilesA = new NativeList<WaterTile>(2048, Allocator.Persistent);
            baseTilesB = new NativeList<WaterTile>(2048, Allocator.Persistent);
            WaterTiles = new NativeList<WaterTile>(8196, Allocator.Persistent);
            TileMatrices = new NativeArray<float4x4>(8196, Allocator.Persistent);
            TileCount = new NativeArray<int>(iterations + 1, Allocator.Persistent);
        }
        
        // funciton to dispose of native arrays if they are created and recreate them
        public void Recreate()
        {
            Cleanup();
            CreateArrays();
        }

        public void GenerateSurface(ref WaterMeshSettings settings, ref Camera cam,
            ref Material material, int layer)
        {
            GenerateSurface(ref settings, ref cam, ref cam, ref material, layer);
        }

        public void GenerateSurface(ref WaterMeshSettings settings, ref Camera cam, ref Camera debugCam,
            ref Material material, int layer)
        {
            if (ProjectSettings.Instance == null)
            {
                Debug.LogError("WaterProjectSettings is missing, skipping water surface generation");
                return;
            }

            if (!WaterTiles.IsCreated || !baseTilesA.IsCreated || !baseTilesB.IsCreated)
            {
                Recreate();
            }

            // TODO - this is jsut a quick hack for now to have control over density
            settings.density = math.max(ProjectSettings.Quality.geometrySettings.density, 0.05f);
            settings.maxDivisions = math.clamp(ProjectSettings.Quality.geometrySettings.maxDivisions, 1, 6);

            var handles = new JobHandle[iterations + 1];
            baseTilesA.Clear();
            WaterTiles.Clear();

            if (settings.infinite)
            {
                var pos = debugCam.transform.position;
                pos.x = Mathf.Round(pos.x / settings.baseTileSize) * settings.baseTileSize;
                pos.z = Mathf.Round(pos.z / settings.baseTileSize) * settings.baseTileSize;
                pos.y = transformMatrix.GetPosition().y;
                transformMatrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            }

            // Setup culling planes
            cullingPlanes = GeometryUtility.CalculateFrustumPlanes(debugCam);
            for (int i = 0; i < cullingPlanes.Length; i++)
            {
                FrustumPlanes[i] = new float4(cullingPlanes[i].normal.x,
                    cullingPlanes[i].normal.y,
                    cullingPlanes[i].normal.z,
                    cullingPlanes[i].distance);
            }

            // Base tiles
            var baseLayout = new BaseLayout()
            {
                TileSize = settings.baseTileSize,
                SurfaceSize = settings.size,
                Output = baseTilesA,
                Count = TileCount,
            };
            handles[0] = baseLayout.Schedule();
            handles[0].Complete();

            if (baseTilesA.Length == 0)
            {
                Debug.Log($"Skipping tile generation due to lack of base tiles.");
            }
            else
            {
                // Subdivision
                var newPoint = math.mul(math.inverse(transformMatrix), new float4(debugCam.transform.position, 1));

                var iterationSize = 2;
                for (int s = 0; s < iterations; s++)
                {
                    //Profiler.BeginSample("Subdivide");
                    baseTilesB.Clear();

                    var subdivideJob = new SubdivideTiles()
                    {
                        BaseTiles = baseTilesA,
                        CullingPlanes = FrustumPlanes,
                        CameraPosition = newPoint.xyz,
                        CameraFov = debugCam.fieldOfView,
                        Settings = settings,
                        IterationLength = iterationSize * (s + 1),
                        TransformationMatrix = transformMatrix,
                        OverflowTiles = baseTilesB.AsParallelWriter(),
                        Tiles = WaterTiles.AsParallelWriter(),
                    };
                    handles[s + 1] = subdivideJob.Schedule(baseTilesA.Length, 1, handles[s]);
                    handles[s + 1].Complete();

                    while (!handles[s + 1].IsCompleted)
                    {
                        return;
                    }

                    // job count for next round
                    //TileCount[s + 1] = baseTilesB.Length;

                    // swap buffer
                    (baseTilesA, baseTilesB) = (baseTilesB, baseTilesA);
                    baseTilesB.Clear();
                    //baseTilesA.CopyFrom(baseTilesB);

                    //Profiler.EndSample();
                }

                // Matrix mul
                var matrixJob = new MatrixJob()
                {
                    Tiles = WaterTiles,
                    Matrices = TileMatrices,
                };
                var matrixJobHandle = matrixJob.Schedule(WaterTiles.Length, 128, handles[^1]);
                matrixJobHandle.Complete();

                //Debug.Log($"{TileCount[0]} base tiles, {WaterTiles.Length} tiles");

                // Material
                settingsShader.x = settings.maxDivisions;
                settingsShader.y = settings.baseTileSize;
                settingsShader.z = settings.density;
                settingsShader.w = settings.maxWaveHeight;
                //material.SetVector("_BAW_Mesh_Settings", settingsShader);
                //material.SetVector("_camPos", debugCam.transform.position);
                material.SetFloat("_CameraFov", debugCam.fieldOfView);
                //material.SetMatrix("_debugCam", debugCam.projectionMatrix);

                Render(ref material, ref cam, ref TileMatrices, WaterTiles.Length, layer);
            }

            //Profiler.EndSample();
        }

        private static void Render(ref Material material, ref Camera camera, ref NativeArray<float4x4> tiles, int count,
            int layer)
        {
            if (ProjectSettings.Instance.resources == null)
            {
                Debug.LogError("Water Mesh Surface wont render due to missing WaterProjectSettings Resources.");
                return;
            }
            // Rendering
            Profiler.BeginSample("Drawing");

            var toDraw = count;
            var batchStart = 0;
            var batchSize = TileTempMatrices.Length;
            do
            {
                if (toDraw - batchSize < 0) batchSize = toDraw;

                Profiler.BeginSample("matrix stuff");
                var arr = tiles.GetSubArray(batchStart, TileTempMatrices.Length).Reinterpret<Matrix4x4>();
                arr.CopyTo(TileTempMatrices);
                Profiler.EndSample();

                Profiler.BeginSample("drawMeshInstanced");

                Graphics.DrawMeshInstanced(
                    ProjectSettings.Instance.resources.WaterTile,
                    0,
                    material,
                    TileTempMatrices,
                    batchSize,
                    null,
                    ShadowCastingMode.Off,
                    true,
                    layer,
                    camera,
                    LightProbeUsage.Off
                );
                Profiler.EndSample();
                batchStart += batchSize;
                toDraw -= batchSize;
            } while (toDraw > 0);

            Profiler.EndSample();
        }

        public void Cleanup()
        {
            if (FrustumPlanes.IsCreated)
                FrustumPlanes.Dispose();
            if (baseTilesA.IsCreated)
                baseTilesA.Dispose();
            if (baseTilesB.IsCreated)
                baseTilesB.Dispose();

            if (WaterTiles.IsCreated)
                WaterTiles.Dispose();
            if (TileMatrices.IsCreated)
                TileMatrices.Dispose();
            if (TileCount.IsCreated)
                TileCount.Dispose();
        }

        [BurstCompile]
        private struct BaseLayout : IJob
        {
            [ReadOnly] public int TileSize;
            [ReadOnly] public float2 SurfaceSize;

            public NativeList<WaterTile> Output;
            public NativeArray<int> Count;

            public void Execute()
            {
                var xCount = math.max((int)math.ceil(SurfaceSize.x / TileSize), 1);
                var xSize = xCount * TileSize;

                var zCount = math.max((int)math.ceil(SurfaceSize.y / TileSize), 1);
                var zSize = zCount * TileSize;

                if (SurfaceSize.y <= float.Epsilon)
                {
                    zCount = xCount;
                    zSize = xSize;
                }

                var estCount = xCount * zCount;

                if (estCount > Output.Capacity)
                {
                    Debug.LogError(
                        $"Too many base tiles({estCount} vs. {Output.Capacity}), reduce the area of water or raise the base tile size.");
                }
                else
                {
                    for (var x = 0; x < xCount; x++)
                    {
                        var xPos = x * TileSize - xSize * 0.5f + TileSize * 0.5f;

                        for (var z = 0; z < zCount; z++)
                        {
                            var zPos = z * TileSize - zSize * 0.5f + TileSize * 0.5f;
                            var tile = new WaterTile
                            {
                                Matrix = float4x4.identity,
                                Division = 0,
                            };
                            tile.Matrix.c3.x = xPos;
                            tile.Matrix.c3.z = zPos;
                            if (SurfaceSize.y <= float.Epsilon && math.distance(tile.Matrix.c3.xz, float2.zero) >
                                (xSize + TileSize) * 0.5f) continue;

                            tile.Matrix.c0.x = tile.Matrix.c1.y = tile.Matrix.c2.z = TileSize; // scale
                            //Output[z + (x * zCount)] = tile;
                            Output.AddNoResize(tile);
                            Count[0]++;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct SubdivideTiles : IJobParallelFor
        {
            [ReadOnly] public NativeList<WaterTile> BaseTiles;
            [ReadOnly] public NativeArray<float4> CullingPlanes;
            [ReadOnly] public WaterMeshSettings Settings;

            [ReadOnly] public float4x4 TransformationMatrix;
            [ReadOnly] public float3 CameraPosition;
            [ReadOnly] public float CameraFov;
            [ReadOnly] public int IterationLength;

            public NativeList<WaterTile>.ParallelWriter Tiles;
            public NativeList<WaterTile>.ParallelWriter OverflowTiles;

            public void Execute(int index)
            {
                SubdivideCheck(BaseTiles[index], TileScale(BaseTiles[index].Division));
            }

            private void SubdivideCheck(WaterTile tile, float size)
            {
                var transformedTile = TransformMatrix(tile.Matrix, TransformationMatrix);
                if (!InFrustum(tile.Matrix, transformedTile, CullingPlanes, Settings.maxWaveHeight)) return;

                // Check if tile has hit max iteration;
                if (tile.Division > IterationLength)
                {
                    // store for next round
                    OverflowTiles.AddNoResize(tile);
                    return;
                }

                // Check if tile needs further division
                var divide = Divide(tile.Matrix.c3.xyz, tile.Matrix.c0.x, CameraPosition, CameraFov);
                if (divide > Settings.density && tile.Division < Settings.maxDivisions)
                {
                    Subdivide(tile);
                }
                else
                {
                    tile.Matrix = transformedTile;
                    tile.Matrix.c0.w = tile.Division;
                    tile.Matrix.c1.w = Settings.density;
                    tile.Matrix.c2.w = size;
                    Tiles.AddNoResize(tile);
                }
            }

            private void Subdivide(WaterTile tile)
            {
                var newTile = new WaterTile
                {
                    Division = tile.Division + 1,
                    Matrix = float4x4.identity,
                };

                var scale = TileScale(newTile.Division);
                newTile.Matrix.c1.y = newTile.Matrix.c0.x = newTile.Matrix.c2.z = scale;
                var offset = scale * 0.5f;
                for (var i = 0; i < 4; i++)
                {
                    newTile.Matrix.c3.xyz = tile.Matrix.c3.xyz;
                    newTile.Matrix.c3.x += i % 2 == 0 ? offset : -offset;
                    newTile.Matrix.c3.z += i < 2 ? offset : -offset;
                    SubdivideCheck(newTile, scale);
                }
            }

            private float TileScale(int division)
            {
                var ratio = math.pow(2, division);
                return Settings.baseTileSize / ratio;
            }
        }

        [BurstCompile]
        private struct MatrixJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<WaterTile> Tiles;
            [WriteOnly] public NativeArray<float4x4> Matrices;

            public void Execute(int index)
            {
                Matrices[index] = Tiles[index].Matrix;
            }
        }


        #region JobHelpers

        private static bool InFrustum(float4x4 matrix, float4x4 transformed, NativeArray<float4> planes, float buffer)
        {
            var size = float3.zero;
            size.x = matrix.c0.x;
            size.y = matrix.c1.y;
            size.z = matrix.c2.z;
            //var size = math.mul(matrix * float4.)
            var bbMin = transformed.c3.xyz - size * 0.5f - buffer;
            var bbMax = transformed.c3.xyz + size * 0.5f + buffer;

            for (var i = 0; i < 6; i++)
            {
                float3 pos;
                pos.x = planes[i].x > 0 ? bbMax.x : bbMin.x;
                pos.y = planes[i].y > 0 ? bbMax.y : bbMin.y;
                pos.z = planes[i].z > 0 ? bbMax.z : bbMin.z;

                if (DistanceToPlane(planes[i], pos) < 0) return false;
            }

            return true;
        }

        private static float DistanceToPlane(float4 plane, float3 position)
        {
            return math.dot(plane.xyz, position) + plane.w;
        }

        private static float Divide(float3 point, float size, float3 cameraPosition, float cameraFov)
        {
            var dist = math.distance(point, cameraPosition);
            var angle = math.degrees(math.atan(size / dist));
            var value = angle / cameraFov;
            return value;
        }

        private static float4x4 TransformMatrix(float4x4 input, float4x4 transform)
        {
            return math.mul(transform, input);
        }

        #endregion


        public struct WaterTile
        {
            public float4x4 Matrix;
            public int Division;
        }

        [Serializable]
        public struct WaterMeshSettings
        {
            [Range(0, 10)] public int maxDivisions;
            [Range(1, 50)] public int baseTileSize;
            public bool infinite;
            public float2 size;

            [Range(0.05f, 1f)] public float density;

            // water info
            public float maxWaveHeight;
        }

        public static WaterMeshSettings NewMeshSettings()
        {
            var settings = new WaterMeshSettings();

            settings.maxDivisions = 4;
            settings.baseTileSize = 20;

            settings.density = 0.2f;
            settings.maxWaveHeight = 1f;

            settings.size = new float2(250, 500);

            return settings;
        }
    }
}
