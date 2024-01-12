using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Simplex;
using Worley;

public abstract class GeneratePlane : MonoBehaviour
{
    public int xVertCount, yVertCount;
    private int radius;
    public Material patchMaterial;
    Color[] regions; //will i run into trouble if this is pointing to a reference?
    float[] heights;
    public PatchConfig patch;

    protected int octaves;
    protected float lacunarity;
    protected float persistance;
    protected int seed;
    protected float scale;

    protected float oceanFloor;
    protected float oceanMulitplier;
    protected float landMultiplier;
    protected float frequency;
    protected float amplitude;

    public abstract float NoiseValue(Vector3 pos, float scale);
    public void Generate(PatchConfig planePatch,float LODstep) {


        
        //ideally, shouldn't this be in the parent object, then every child references it?

        MeshFilter mf = this.gameObject.AddComponent<MeshFilter>();
        MeshRenderer rend = this.gameObject.AddComponent<MeshRenderer>();

        rend.sharedMaterial = patchMaterial;
        Mesh m = mf.sharedMesh = new Mesh();
        patch = planePatch;

        xVertCount = planePatch.vertices.x;
        yVertCount = planePatch.vertices.y;
        radius = 1;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + planePatch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVertCount - 1), 1f / (yVertCount - 1)); //determines the distance or "step" amount between vertices
        step *= LODstep; //make smaller steps if higher lod


        Vector3[] vertices = new Vector3[xVertCount * yVertCount];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];

        Texture2D tex = new Texture2D(xVertCount, yVertCount);


        //GET NECESSARY VALUES FOR NOISE FROM PARENT PLANET
        //octaves = planePatch.planetObject.GetComponent<Sphere>().getOctaves();
        //lacunarity = planePatch.planetObject.GetComponent<Sphere>().getLacunarity();
        //persistance = planePatch.planetObject.GetComponent<Sphere>().getPersistance();
        seed = planePatch.planetObject.GetComponent<Sphere>().getSeed();
        //scale = planePatch.planetObject.GetComponent<Sphere>().getScale();

        //oceanFloor = planePatch.planetObject.GetComponent<Sphere>().getOceanFloor();
        //oceanMulitplier = planePatch.planetObject.GetComponent<Sphere>().getOceanMultiplier();
        //landMultiplier = planePatch.planetObject.GetComponent<Sphere>().getLandMultiplier();

        /*
        noise = new Noise(); //creates new simplex noise object for later use
        noise.Seed = seed;*/

        //Vector3 worleyPosition = planePatch.planetObject.transform.position;
        //definitely not optimized, optimize later

        //worleyNoise = new WorleyNoise(false);
        //worleyNoise.Seed = seed;

        //generates Simplex Noise and stores in 3d array

        for (int y = 0; y < yVertCount; y++)
        {
            for (int x = 0; x < xVertCount; x++)
            {

                int i = x + y * xVertCount;
                Vector2 p = offset + new Vector2(x * step.x, y * step.y); //determines vertex location in grid

                uvs[i] = p + Vector2.one * 0.5f;
                Vector3 vec = ((planePatch.uAxis * p.x) + (planePatch.vAxis * p.y) + (planePatch.height * 0.5f)); //determine plane vertex based on direction. p determines
                                                                                                                  //vertex location in grid

                float noiseHeight = 0f; // should return a value between 0 and 1
                vec = vec.normalized; //makes it a sphere

                float range = 1f;

                OctaveNoise(vec, ref range, ref noiseHeight, seed, scale, octaves, lacunarity, persistance);
                
                float addHeight = (noiseHeight > oceanFloor) ? (noiseHeight*landMultiplier) : (noiseHeight * oceanMulitplier);
                
                //change vertex according to height map curve
                vec *= (1.0f + addHeight);
                float currentHeight = noiseHeight / range;

                normals[i] = vec;
                vertices[i] = vec * radius;

                //SET TEXTURE PIXELS ACCORDINGLY

                createPatchTexture(ref tex, x, y, currentHeight);

            }
        }

        tex.Apply();
        tex.alphaIsTransparency = true;
        tex.filterMode = FilterMode.Point;
        
        transform.GetComponent<Renderer>().material.mainTexture = tex;
        transform.GetComponent<Renderer>().material.mainTextureScale = new Vector2(1 << patch.LODlevel, 1 << patch.LODlevel);

        //SET INDICES FOR THE MESH
        int[] indices = new int[(xVertCount - 1) * (yVertCount - 1) * 4];
        for (int y = 0; y < yVertCount - 1; y++)
        {
            for (int x = 0; x < xVertCount - 1; x++)
            {
                int i = (x + y * (xVertCount - 1)) * 4;
                indices[i] = x + y * xVertCount;
                indices[i + 1] = x + (y + 1) * xVertCount;
                indices[i + 2] = x + 1 + (y + 1) * xVertCount;
                indices[i + 3] = x + 1 + y * xVertCount;
            }
        }
        m.vertices = vertices;
        m.normals = normals;
        m.uv = uvs;
        m.SetIndices(indices, MeshTopology.Quads, 0);
        m.RecalculateBounds();

        mf.sharedMesh.SetTriangles(mf.sharedMesh.GetTriangles(0), 0);

        this.gameObject.AddComponent<MeshCollider>();

    }

    float ExponentialDistribution(float lambda, float x) {
        return (x > 0) ? lambda * Mathf.Exp(-(x * lambda)) : 0;
    }

    protected abstract void createPatchTexture(ref Texture2D tex, int x, int y, float currentHeight);

    float OctaveNoise(Vector3 vec,ref float range, ref float noiseHeight, int seed, float scale, int octaves, float lacunarity, float persistance)
    {

        frequency = 1;
        amplitude = 1;

        for (int g = 0; g < octaves; g++)
        {
            
            //THIS LINE HERE WILL CHANGE TO ACCOMODATE ADDITIONAL ALGORITHMS
            float perlinValue = NoiseValue(vec, scale);

            noiseHeight += perlinValue * amplitude;

            amplitude *= persistance;
            frequency *= lacunarity;
            range += amplitude / 4;
        }
        return noiseHeight;
    }

    public static float Perlin3d(float x, float y, float z)
    {
        float AB = Mathf.PerlinNoise(x, y);
        float BC = Mathf.PerlinNoise(y, z);
        float AC = Mathf.PerlinNoise(x, z);

        float BA = Mathf.PerlinNoise(y, x);
        float CB = Mathf.PerlinNoise(z, y);
        float CA = Mathf.PerlinNoise(z, x);

        float ABC = AB + BC + AC + BA + CB + CA;

        return ABC / 6f;
    }

    public Vector3 getPosition(PatchConfig planePatch, float LODstep) { //returns the middle vertex position. Is used to
                                                                        //measure distance for LOD

        float xVert = planePatch.vertices.x;
        float yVert = planePatch.vertices.y;

        Vector2 offset = new Vector2(-0.5f, -0.5f) + planePatch.LODOffset; //to center all side meshes. Multiply by LODoffset to give correct quadrant
        Vector2 step = new Vector2(1f / (xVert - 1), 1f / (yVert - 1)); //determines the distance or "step" amount between vertices
        step *= LODstep; //make smaller steps if higher lod

        Vector2 p = offset + new Vector2( (xVert/2) * step.x, (yVert/2) * step.y);

        Vector3 vec = ((planePatch.uAxis * p.x) + (planePatch.vAxis * p.y) + (planePatch.height * 0.5f));
        vec = vec.normalized;

        vec += this.transform.position;
        return vec;
    }
}
