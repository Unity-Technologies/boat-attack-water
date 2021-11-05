#ifndef INFINITE_WATER_INCLUDED
#define INFINITE_WATER_INCLUDED

#define EPSILON 0.00001

float _CameraRoll;
float _Size;

struct InfinitePlane
{
    float3 hit;
    float3 positionWS;
    float depth;
};

// ray-plane intersection test
// @return side of plane hit
//    0 : no hit
//    1 : front
//    2 : back
int intersect_plane (float3 ro, float3 rd, float3 po, float3 pd, out float3 hit)
{
    float D = dot(po, pd);       // re-parameterize plane to normal + distance
    float tn = D - dot(ro, pd);  // ray pos w.r.t. plane (front, back, on)
    float td = dot (rd, pd);     // ray ori w.r.t. plane (towards, away, parallel)

    if (td > -EPSILON  &&  td < EPSILON)  return 0;  // parallel to plane

    float t = tn / td;          // dist along ray to hit
    if (t < 0.0)  return 0;     // plane lies behind ray
    hit = ro + t * rd;          // got a hit
    return (tn > 0.0) ? 2 : 1;  // which side of the plane?
}

InfinitePlane WorldPlane(float4 positionSS, float4 viewDirection)
{
    InfinitePlane output;

    float4 p = positionSS;
    float2 uv = p.xy / p.w; // [0, 1]
    half2 st = 2.0 * uv - half2(1.0, 1.0);
    float asp =  _ScreenParams.x /  _ScreenParams.y;
    float2 st_adj = float2(st.x * asp, st.y);

    // camera settings
    float3 cameraPosition = GetCameraPositionWS();
    float3 cam_ori = float3(-cameraPosition.x, cameraPosition.y, cameraPosition.z);
    float3 cam_dir = float3(viewDirection.x, -viewDirection.y, -viewDirection.z);

    // over, up, norm basis vectors for camera
    half zRot = radians(-_CameraRoll);
    float3 cam_ovr = normalize(cross(cam_dir, half3(0, cos(zRot), sin(zRot))));
    float3 cam_uhp = normalize(cross(cam_ovr, cam_dir));

    // height offset
    half offsetY = _WaveHeight;// - _MaxWaveHeight;
    
    // scene
    float3 planeOrigin = half3(0.0, offsetY, 0.0);
    float3 planeDirection = half3(0.0, 1.0, 0.0);

    // ray
    half3 rayOrigin = cam_ori;
    float cam_dist = unity_CameraProjection._m11;
    float3 rayTransform = cam_ori + cam_dist*cam_dir;
    rayTransform += st_adj.x * cam_ovr;
    rayTransform += st_adj.y * cam_uhp;
    half3 rayDirection = normalize(rayTransform - cam_ori);

    int side = intersect_plane (rayOrigin, rayDirection, planeOrigin, planeDirection, output.hit);
    if(side == 0)
        discard;

    // plane
    output.positionWS = float3(-dot(output.hit, half3(1, 0, 0)), offsetY, dot(output.hit, half3(0, 0, 1)));

    // re-construct depth
    float4 clipPos = TransformWorldToHClip(output.positionWS);
    output.depth = clipPos.z / clipPos.w;
    
    return output;
}

#endif //INFINITE_WATER_INCLUDED