// Real-world occlusion for mixed reality: draws the scanned room (the PICO spatial mesh) into depth only,
// writing no color. Rendered before the furniture, it hides any virtual surface behind a real one, and
// the untouched transparent background lets passthrough show there — a real plant stays in front of a shelf.
Shader "PaletteRoom/Passthrough Occluder"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Occluder"
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off
            ColorMask 0 // depth only: the screen keeps its clear (transparent) color, so passthrough shows

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Set by SceneObstacles: the floor's world height.
            float _PaletteRoomFloorY;
            // The scanned floor is bumpy by a few cm; anything this close to the floor is the floor itself.
            #define FLOOR_BAND 0.06
            // ...and is drawn this far below it, so rugs and furniture feet lying on the floor aren't cut.
            #define FLOOR_SINK 0.03

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // A hair farther away than the scan, so real surfaces don't flicker against pieces touching them.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (positionWS.y < _PaletteRoomFloorY + FLOOR_BAND)
                    positionWS.y = _PaletteRoomFloorY - FLOOR_SINK;
                float3 toCamera = normalize(GetCameraPositionWS() - positionWS);
                output.positionCS = TransformWorldToHClip(positionWS - toCamera * 0.02);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(0, 0, 0, 0); // transparent: the passthrough shows through
            }
            ENDHLSL
        }
    }
}
