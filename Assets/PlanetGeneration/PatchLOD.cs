using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class PatchLOD {

    // HAVE ALL PATCHES BE PART OF A TREE THAT THE PLANET KEEPS TRACK OF
    private List<PatchLOD> childNode;

    private GameObject patch;
    private PatchConfig patchConfig; //configuration data for the patch
    private PatchLOD parent;
    private Vector3 position;
    
    public PatchLOD(GameObject patch, PatchLOD parent) {
        this.patch = patch;
        patchConfig = patch.GetComponent<GeneratePlane>().patch;
        childNode = new List<PatchLOD>() { };
        this.parent = parent;

        //the complicated math part should be refactored, it should be consolidated
        this.position = patch.GetComponent<GeneratePlane>().getPosition(patchConfig, 1f / (1 << (patchConfig.LODlevel))  );
        //needs gameObject plane, plane needs resolution, start position
    }


    public Vector3 getPosition() { return position; }

    public void AddChild(GameObject patchChild)
    {
        PatchLOD newNode = new PatchLOD(patchChild, this);
        childNode.Add(newNode);
    }

    public PatchLOD getChild(int childIndex)
    {
        if (childNode.Count > 0)
        {
            return childNode[childIndex];
        }
        else { return null; }
    }

    public PatchLOD getParent()
    {
        return this.parent;
    }

    public GameObject getPatch(int planetIndex)
    {
        return patch;
    }

    public void nextLOD() {

        patch.transform.GetComponent<MeshRenderer>().enabled = false;
        patch.transform.GetComponent<MeshCollider>().enabled = false;

        //PatchConfig patchConfigChild = new PatchConfig("NorthWest", patchConfig.uAxis, patchConfig.vAxis, patchConfig.LODlevel + 1);
        string[] names = {"NorthWest", "NorthEast", "SouthWest","SouthEast" };
        Vector2[] binaryOperator = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };

        for (int i = 0; i < names.Length; ++i)
        {
            GameObject patchChild = new GameObject(names[i] + "_Level_" + patchConfig.LODlevel + 1);

            patchChild.AddComponent<GeneratePlane>();

            patchChild.transform.parent = patch.transform;
            patchChild.transform.localEulerAngles = Vector3.zero; //zero out local position and rotation
            patchChild.transform.localPosition = Vector3.zero;
            
            float powerof2Frac = 1f/ (1 << (patchConfig.LODlevel+1)); //maybe +1?

            Vector2 LODOffset = patchConfig.LODOffset;
            LODOffset += (new Vector2(powerof2Frac, powerof2Frac) * binaryOperator[i]);
            ////////////////////////////////////////////////////////////////

            //make new patch config
            float newDistanceThreshold;
            if (patchConfig.LODlevel >= 5)
            {
                newDistanceThreshold = 0;
            }
            else {
                newDistanceThreshold = patchConfig.distanceThreshold / 2f;
            }
            

            PatchConfig patchConfigChild = new PatchConfig(names[i], patchConfig.uAxis, patchConfig.vAxis, patchConfig.LODlevel + 1, LODOffset, patchConfig.vertices,patchConfig.planetObject,newDistanceThreshold);


            //have patchConfig child inherit everything from parent
            patchChild.GetComponent<GeneratePlane>().Generate(patchConfigChild,powerof2Frac);
            AddChild(patchChild);
        }
    }

    public void prevLOD(PatchLOD node) {
        //do 2DFS, first one deletes all leaf nodes and corresponding objects
        //2nd one turns back on the mesh renderer and collider of new leaf
        deleteLeafDFS(node);
        turnOnLeafMeshDFS(node);
    }

    public void turnOnLeafMeshDFS(PatchLOD node) {
        if (node.childNode.Count > 0)
        {
            for (int i = 0; i < node.childNode.Count; ++i)
            { //traverses DFS 
                turnOnLeafMeshDFS(node.childNode[i]);

            }
        }
        else
        {
            //if no children, then turn on mesh renderer and mesh collider
            node.patch.GetComponent<MeshRenderer>().enabled = true;
            node.patch.GetComponent<MeshCollider>().enabled = true;
        }
    }

    public void deleteLeafDFS(PatchLOD node) {
        if (node.childNode.Count == 0) {
            node.parent.childNode.Remove(node);
            GameObject.Destroy(node.patch);
            return;
        }
        else {
            for (int i = node.childNode.Count-1; i >= 0; --i)
            { //traverses DFS 
                deleteLeafDFS(node.childNode[i]);
            }
        }
    }

    public void traverseAndGenerate(PatchLOD node) //used for finding the leaf nodes and then using nextLOD to actually generate
    {
        if (node.childNode.Count > 0)
        {
            for (int i = 0; i < node.childNode.Count; ++i)
            { //traverses DFS 
                traverseAndGenerate(node.childNode[i]);

            }
        }
        else {
            node.nextLOD(); //if no children, then activates nextLOD functions of lowest level children
        }

    }

    public void LODbyDistance(PatchLOD node, GameObject player) //used for finding the leaf nodes and then using nextLOD to actually generate
    {
        if (node.childNode.Count > 0)
        {
            for (int i = 0; i < node.childNode.Count; ++i)
            { //traverses DFS 
                LODbyDistance(node.childNode[i],player);

            }
        }
        else
        {
            //need to take into account that all patches have the same location, but are offset differently
            float distance = Vector3.Distance(node.position, player.transform.position);
            if (distance < node.patchConfig.distanceThreshold)
            {
                node.nextLOD();
            }else if (distance > 4* node.patchConfig.distanceThreshold) //if distance between player and patch is too large
                                                                        //then undo the LOD
            {
                node.prevLOD(node.parent);

            }
        }

    }
}

