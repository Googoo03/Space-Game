

Shader "Custom/Atmosphere_IE"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PlanetPos ("Planet Position", Vector) = (0,0,0,0)
        _SunPos ("Sun Position", Vector) = (0,0,0,0)
        _Color ("Atmosphere Color", Color) = (1,1,1,1)
        _Color2 ("Sunset Color", Color) = (1,1,1,1)
        _Radius ("Planet Radius", float) = 5
        _AtmosphereHeight ("Atmosphere Height", float) = 0.5
        _Density ("Atmosphere Density", float) = 1
    }
    SubShader
    {
        // No culling or depth
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 viewVector : TEXCOORD2;
                float3 camRelativeWorldPos : TEXCOORD3;
                float3 worldNormal : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            v2f vert (appdata_base v)
            {
                v2f o;

                float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(v.normal, 0)).xyz);

                o.worldNormal = worldNormal;
                o.vertex = UnityObjectToClipPos(v.vertex);
                //o.uv = v.uv;

                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = (o.screenPos.xy/o.screenPos.w);

                float3 viewVector = mul(unity_CameraInvProjection, float4((o.screenPos.xy/o.screenPos.w) * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                o.camRelativeWorldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz - _WorldSpaceCameraPos;
                

                return o;
            }


            sampler2D _MainTex;
            float3 _PlanetPos;
            float3 _SunPos;
            float _Radius;
            float _AtmosphereHeight;
            float _Density;
            float4 _Color;
            float4 _Color2;

            float distanceToPlanet;

            RWStructuredBuffer<float3> planetPositions;
            float numPlanets;


            float3 orthogonalProjection(float3 mu, float3 v, float cameraDistanceToPlanet) //calculates the orthogonal projection of v onto mu, returns the orthogonal vector
            {
                float lengthV = length(v);
                float scalar = (dot(mu,v)/(lengthV*lengthV));
                float3 w1 = scalar * v;
                float3 w2 = mu - w1;
                return (scalar < 0 && cameraDistanceToPlanet > _Radius+_AtmosphereHeight) ? w2 * 1000 : w2;
            }

            float QuadraticSolve(float a, float b, float c, bool plus){
                return plus ? ( (-b + sqrt(b*b - 4*a*c)) / 2*a) : ( (-b - sqrt(b*b - 4*a*c)) / 2*a);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                
                float3 viewDirection = normalize(i.viewVector);
                //set vector from camera to planet and get magnitude of said vector
                float3 cameraToPlanetVector =  _PlanetPos - _WorldSpaceCameraPos;
                float cameraDistanceToPlanet = length(cameraToPlanetVector);
                

                float depthTextureSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float terrainLevel = LinearEyeDepth(depthTextureSample);

                

                

                fixed4 noColor = tex2D(_MainTex, i.uv);
               
                float atmosphereAlpha = 1;

                

                /////////ONLY SHOW WHERE INTERSECTS WITH LIGHT
                float r = _Radius + _AtmosphereHeight;
                float3 Q = _WorldSpaceCameraPos -  _PlanetPos;
                float a = 1;//viewDirection * viewDirection;
                float b = 2 * dot(viewDirection, Q);
                float c = dot(Q,Q) - r*r;
                float d = (dot(viewDirection,Q)*dot(viewDirection,Q)) - c;

                float t1 = -1;
                float t2 = -1;
                
                float dotProduct = 0;
                float atmosphere_depth = 0;

                fixed4 col;

                if(d >= 0){
                    t1 = QuadraticSolve(a,b,c,false);
                    t2 = QuadraticSolve(a,b,c,true);

                    if(t1 >= 0 || t2 >=0){

                        float t3;// = t1 >= 0 ? t1 : t2;
                        if(t1 > 0 && t2 > 0){
                            t3 = min(t1,t2);
                        }else { t3 = t1 >= 0 ? t1 : t2;}
                        float3 intersectionPoint = _WorldSpaceCameraPos + (t3*viewDirection);
                        distanceToPlanet = length(intersectionPoint-_PlanetPos);
                        //atmosphereAlpha *= 1 - saturate((distanceToPlanet - _Radius) / _AtmosphereHeight);
                        //atmosphere_depth = clamp(terrainLevel - intersection_atmosphere_scalar,0,_AtmosphereHeight);

                    
                    
                        float3 start_point;
                        float3 end_point;

                        //throw out if outside the circle and terrain is in front
                        if(terrainLevel < t3 && t1>0 && t2>0) return noColor;

                        start_point = t3 == t1 ? intersectionPoint : _WorldSpaceCameraPos;

                        float3 viewPlane = i.camRelativeWorldPos.xyz / dot(i.camRelativeWorldPos.xyz, unity_WorldToCamera._m20_m21_m22);
 
                        // calculate the world position
                        // multiply the view plane by the linear depth to get the camera relative world space position
                        // add the world space camera position to get the world space position from the depth texture
                        float3 terrainworldPos = viewPlane * terrainLevel + _WorldSpaceCameraPos;

                        end_point = t2 > (terrainLevel) ? _WorldSpaceCameraPos+(terrainLevel*viewDirection) : _WorldSpaceCameraPos+(t2*viewDirection);

                    
                    
                        float3 normalVector = normalize(end_point -  _PlanetPos); 
                        float3 lightVector = normalize(_SunPos -  _PlanetPos);
                        dotProduct = dot(lightVector,normalVector);

                        atmosphere_depth = length(end_point-start_point);
                        atmosphere_depth = atmosphere_depth*atmosphere_depth;

                         fixed4 atmosphereColor = lerp(_Color,_Color2,max(0,dot(lightVector,viewDirection) * saturate(atmosphere_depth) ));
                        atmosphereAlpha *= max(0,dotProduct);

                        col = lerp( fixed4(atmosphereColor.xyz,atmosphereAlpha),noColor,exp(-atmosphere_depth * _Density*atmosphereAlpha) );
                    }
                    
                }else{
                    col = noColor;
                }
               

                return col;
            }
            ENDCG
        }
    }
}

