// Based off Plane Intersection implementation from https://kelvinvanhoorn.wordpress.com/2021/05/11/math-line-intersections/#1.3-plane-intersection

#ifndef INFINITE_WATER_INCLUDED
#define INFINITE_WATER_INCLUDED

struct InfinitePlane
{
    float3 hit;
    float3 positionWS;
    float depth;
};

float3x3 constructTransitionMatrix(float3 forwardDir, float3 upDir)
{
    float3 rightDir = cross(forwardDir, upDir);
    float3x3 result = {rightDir, upDir, forwardDir};
    return result;
}

// Based on plane equation from https://en.wikipedia.org/wiki/Plane_(geometry)#Point%E2%80%93normal_form_and_general_form_of_the_equation_of_a_plane
float intersectPlane(float3 lineOrigin, float3 lineDir, float3 shapeOrigin, float3 shapeUpDir)
{
    // Transform line origin and direction from world space to the shape space
    float3x3 transitionMatrix = constructTransitionMatrix(float3(0,0,0), shapeUpDir);
    float3 lO = mul(transitionMatrix, lineOrigin - shapeOrigin);
    float3 lD = mul(transitionMatrix, lineDir);
 
    float denominator = lD.y;
    float numerator = lO.y;
     
    return - numerator / denominator;
}

InfinitePlane WorldPlane(float4 viewDirection, float3 positionWS)
{
    InfinitePlane output = (InfinitePlane)0;

    half3 offset = float3(0, _WaveHeight, 0);
    // Line information
    float3 lineOrigin = _WorldSpaceCameraPos - offset;
    float3 lineDir = normalize(positionWS - _WorldSpaceCameraPos);
 
    // Shape information
    float3 shapeUpDir = normalize(mul(unity_ObjectToWorld, float4(0,1,0,0)).xyz);
    float3 shapeOrigin = viewDirection;
    
    // Intersect information
    float intersect = intersectPlane(lineOrigin, lineDir, shapeOrigin, shapeUpDir);

    float3 pos = lineOrigin - shapeOrigin + lineDir * intersect;
    output.positionWS = pos + offset;

    // re-construct depth
    float4 clipPos = TransformWorldToHClip(output.positionWS);
    output.depth = clipPos.z / clipPos.w;

    return output;
}

#endif //INFINITE_WATER_INCLUDED