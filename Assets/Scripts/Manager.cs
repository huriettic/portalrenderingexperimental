using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.Serialization.Json;

public class Manager : MonoBehaviour
{
    public string Name;

    public float lerpX;
    public float lerpY;
    public float snap = 25f;
    public float rotationX;
    public float rotationY;
    public float lookAngle = 90f;
    public float sensitivityX = 10f;
    public float sensitivityY = 10f;
    public float speed = 6f;
    public float jumpHeight = 8f;
    public float gravity = 20f;
    public CharacterController Player;
    private Vector3 moveDirection = Vector3.zero;

    public Camera Cam;

    private Vector4[] PlanePos;

    public MaterialPropertyBlock BlockOne;

    public List<GameObject> LevelObjects = new List<GameObject>();

    public List<GameObject> PlayerStarts = new List<GameObject>();

    public List<Plane> CamPlanes = new List<Plane>(6);

    public List<Plane> Planes = new List<Plane>();

    public Polyhedron CurrentSector;

    public List<Face> Faces = new List<Face>();

    public List<FaceMesh> FaceMeshes = new List<FaceMesh>();

    public List<Polyhedron> Polyhedrons = new List<Polyhedron>();

    public List<Polyhedron> Sectors = new List<Polyhedron>();

    public List<Polyhedron> VisitedSector = new List<Polyhedron>();

    public List<GameObject> LevelMeshes = new List<GameObject>();

    [System.Serializable]
    public class Polyhedron
    {
        public List<int> Planes;
        public List<int> Portal;
        public List<int> Render;
        public List<int> Collision;

        public Polyhedron(List<int> planelist, List<int> portallist, List<int> renderlist, List<int> collisionlist)
        {
            Planes = planelist;
            Portal = portallist;
            Render = renderlist;
            Collision = collisionlist;
        }
    }

    [System.Serializable]
    public class Face
    {
        public int Plane;
        public int Portal;
        public int Render;
        public int Collision;
        public int FaceMesh;
        public List<Vector3> Vertices;

        public Face(int plane, int portal, int render, int collision, int facemesh, List<Vector3> verticeslist)
        {
            Plane = plane;
            Portal = portal;
            Render = render;
            FaceMesh = facemesh;
            Collision = collision;
            Vertices = verticeslist;
        }
    }

    [System.Serializable]
    public class FaceMesh
    {
        public List<int> Triangles;
        public List<Vector2> UVs;

        public FaceMesh(List<int> triangleslist, List<Vector2> uvlist)
        {
            Triangles = triangleslist;
            UVs = uvlist;
        }
    }

    void Awake()
    {
        Load();

        GetlLists();

        HidePolygons();

        AddMeshCollider();

        CreatePolygonPlane();
    }

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        PlanePos = new Vector4[20];

        BlockOne = new MaterialPropertyBlock();

        Playerstart();

        Player.GetComponent<CharacterController>().enabled = true;
    }

    // Update is called once per frame
    void Update()
    {
        Controller();

        Sectors.Clear();

        GetPolyhedrons(CurrentSector);

        CamPlanes.Clear();

        ReadFrustumPlanes(Cam, CamPlanes);

        CamPlanes.RemoveAt(5);

        CamPlanes.RemoveAt(4);

        VisitedSector.Clear();

        GetPortals(CamPlanes, CurrentSector);
    }

    public void Load()
    {
        string LoadLevel = Resources.Load<TextAsset>(Name).text;

        JsonUtility.FromJsonOverwrite(LoadLevel, this);
    }

    public void HidePolygons()
    {
        Shader shader = Shader.Find("Custom/Clipping");

        for (int i = 0; i < Polyhedrons.Count; i++)
        {
            for (int e = 0; e < Polyhedrons[i].Render.Count; e++)
            {
                LevelMeshes[Faces[Polyhedrons[i].Render[e]].FaceMesh].GetComponent<Renderer>().enabled = false;
                LevelMeshes[Faces[Polyhedrons[i].Render[e]].FaceMesh].GetComponent<Renderer>().material.shader = shader;
            }
        }
    }

    public void Playerstart()
    {
        for (int i = 0; i < LevelObjects.Count; i++)
        {
            if (LevelObjects[i].name.Contains("Player"))
            {
                PlayerStarts.Add(LevelObjects[i]);
            }
        }

        int random = UnityEngine.Random.Range(0, PlayerStarts.Count);

        for (int i = 0; i < Polyhedrons.Count; i++)
        {
            if (PlayerStarts[random].name.Contains(Convert.ToString(i)))
            {
                CurrentSector = Polyhedrons[i];

                Player.transform.position = new Vector3(PlayerStarts[random].transform.position.x, PlayerStarts[random].transform.position.y + 1.10f, PlayerStarts[random].transform.position.z);
            }
        }
    }

    private Plane FromVec4(Vector4 aVec)
    {
        Vector3 n = aVec;
        float l = n.magnitude;
        return new Plane(n / l, aVec.w / l);
    }

    public void SetFrustumPlanes(List<Plane> planes, Matrix4x4 m)
    {
        if (planes == null)
            return;
        var r0 = m.GetRow(0);
        var r1 = m.GetRow(1);
        var r2 = m.GetRow(2);
        var r3 = m.GetRow(3);

        planes.Add(FromVec4(r3 - r0)); // Right
        planes.Add(FromVec4(r3 + r0)); // Left
        planes.Add(FromVec4(r3 - r1)); // Top
        planes.Add(FromVec4(r3 + r1)); // Bottom
        planes.Add(FromVec4(r3 - r2)); // Far
        planes.Add(FromVec4(r3 + r2)); // Near
    }

    public void ReadFrustumPlanes(Camera cam, List<Plane> planes)
    {
        SetFrustumPlanes(planes, cam.projectionMatrix * cam.worldToCameraMatrix);
    }

    public void GetlLists()
    {
        GameObject FFaces = GameObject.Find("Faces");

        for (int i = 0; i < FFaces.gameObject.transform.childCount; i++)
        {
            LevelMeshes.Add(FFaces.gameObject.transform.GetChild(i).gameObject);
        }

        GameObject FObjects = GameObject.Find("MapObjects");

        for (int i = 0; i < FObjects.gameObject.transform.childCount; i++)
        {
            LevelObjects.Add(FObjects.gameObject.transform.GetChild(i).gameObject);
        }
    }

    public void AddMeshCollider()
    {
        for (int i = 0; i < Polyhedrons.Count; i++)
        {
            for (int e = 0; e < Polyhedrons[i].Collision.Count; e++)
            {
                LevelMeshes[Faces[Polyhedrons[i].Collision[e]].FaceMesh].AddComponent<MeshCollider>();
            }    
        }
    }

    public void CreatePolygonPlane()
    {
        for (int i = 0; i < Faces.Count; i++)
        {
            Vector3 p1 = Faces[i].Vertices[0];
            Vector3 p2 = Faces[i].Vertices[1];
            Vector3 p3 = Faces[i].Vertices[2];

            Planes.Add(new Plane(p1, p2, p3));
        }
    }

    public float PointDistanceToPlane(Plane plane, Vector3 point)
    {
        return plane.normal.x * point.x + plane.normal.y * point.y + plane.normal.z * point.z + plane.distance;
    }

    public void CreateClippingPlanes(List<Vector3> aVertices, List<Plane> aList, Vector3 aViewPos)
    {
        int count = aVertices.Count;
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            var p1 = aVertices[i];
            var p2 = aVertices[j];
            var n = Vector3.Cross(p1 - p2, aViewPos - p2);
            var l = n.magnitude;
            if (l < 0.01f)
                continue;
            aList.Add(new Plane(n / l, aViewPos));
        }
    }

    public void Controller()
    {
        if (Input.GetKey("escape"))
        {
            Application.Quit();
        }

        if (Input.GetButton("Jump") && Player.isGrounded)
        {
            moveDirection.y = jumpHeight;
        }
        else
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        rotationY += mouseX;
        rotationX -= mouseY;

        rotationX = Mathf.Clamp(rotationX, -lookAngle, lookAngle);
        lerpX = Mathf.Lerp(lerpX, rotationX, snap * Time.deltaTime);
        lerpY = Mathf.Lerp(lerpY, rotationY, snap * Time.deltaTime);

        Camera.main.transform.rotation = Quaternion.Euler(lerpX, lerpY, 0);
        transform.rotation = Quaternion.Euler(0, lerpY, 0);

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        Player.Move(move * speed * Time.deltaTime);
        Player.Move(moveDirection * Time.deltaTime);
    }

    public List<Vector3> ClippingPlane(List<Vector3> invertices, Plane aPlane, float aEpsilon = 0.001f)
    {
        List<Vector3> outvertices = new List<Vector3>();
        List<float> m_Dists = new List<float>();

        int count = invertices.Count;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = invertices[i];
            m_Dists.Add(PointDistanceToPlane(aPlane, p));
        }
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            float d1 = m_Dists[i];
            float d2 = m_Dists[j];
            Vector3 p1 = invertices[i];
            Vector3 p2 = invertices[j];
            bool split = d1 > aEpsilon;
            if (split)
            {
                outvertices.Add(p1);
            }
            else if (d1 > -aEpsilon)
            {
                // point on clipping plane so just keep it
                outvertices.Add(p1);
                continue;
            }
            // both points are on the same side of the plane
            if ((d2 > -aEpsilon && split) || (d2 < aEpsilon && !split))
            {
                continue;
            }
            float d = d1 / (d1 - d2);
            outvertices.Add(p1 + (p2 - p1) * d);
        }
        return outvertices;
    }

    public List<Vector3> ClippingPlanes(List<Vector3> invertices, List<Plane> aPlanes)
    {
        for (int i = 0; i < aPlanes.Count; i++)
        {
            invertices = new List<Vector3>(ClippingPlane(invertices, aPlanes[i]));
        }
        return invertices;
    }

    public bool CheckRadius(Polyhedron asector, Vector3 campoint)
    {
        bool PointIn = true;

        for (int e = 0; e < asector.Planes.Count; e++)
        {
            if (PointDistanceToPlane(Planes[asector.Planes[e]], campoint) < -0.6f)
            {
                PointIn = false;
                break;
            }
        }
        return PointIn;
    }

    public bool CheckPolyhedron(Polyhedron asector, Vector3 campoint)
    {
        bool PointIn = true;

        for (int i = 0; i < asector.Planes.Count; i++)
        {
            if (PointDistanceToPlane(Planes[asector.Planes[i]], campoint) < 0)
            {
                PointIn = false;
                break;
            }
        }
        return PointIn;
    }

    public void GetPolyhedrons(Polyhedron ASector)
    {
        Vector3 CamPoint = Cam.transform.position;

        Sectors.Add(ASector);

        for (int i = 0; i < ASector.Portal.Count; ++i)
        {
            Face f = Faces[ASector.Portal[i]];

            bool t = CheckRadius(Polyhedrons[f.Portal], CamPoint);

            if (Sectors.Contains(Polyhedrons[f.Portal]))
            {
                continue;
            }

            if (t == true)
            {
                GetPolyhedrons(Polyhedrons[f.Portal]);

                continue;
            }
        }

        bool p = CheckPolyhedron(ASector, CamPoint);

        if (p == true)
        {
            CurrentSector = ASector;

            IEnumerable<Polyhedron> except = Polyhedrons.Except(Sectors);

            foreach (Polyhedron sector in except)
            {
                foreach (int collision in sector.Collision)
                {
                    Physics.IgnoreCollision(Player, LevelMeshes[Faces[collision].FaceMesh].GetComponent<MeshCollider>(), true);
                }
            }

            foreach (Polyhedron sector in Sectors)
            {
                foreach (int collision in sector.Collision)
                {
                    Physics.IgnoreCollision(Player, LevelMeshes[Faces[collision].FaceMesh].GetComponent<MeshCollider>(), false);
                }
            }
        }
    }

    public void GetPortals(List<Plane> APlanes, Polyhedron BSector)
    {
        Vector3 CamPoint = Cam.transform.position;

        VisitedSector.Add(BSector);

        Array.Clear(PlanePos, 0, 20);

        for (int i = 0; i < APlanes.Count; i++)
        {
            PlanePos[i] = new Vector4(APlanes[i].normal.x, APlanes[i].normal.y, APlanes[i].normal.z, APlanes[i].distance);
        }

        BlockOne.SetInt("_Int", APlanes.Count);

        BlockOne.SetVectorArray("_Plane", PlanePos);

        for (int i = 0; i < BSector.Render.Count; i++)
        {
            GameObject r = LevelMeshes[Faces[BSector.Render[i]].FaceMesh];

            float d = PointDistanceToPlane(Planes[BSector.Render[i]], CamPoint);

            if (d < -0.1f)
            {
                continue;
            }

            Matrix4x4 matrix = Matrix4x4.TRS(r.transform.position, r.transform.rotation, r.transform.lossyScale);

            Graphics.DrawMesh(r.GetComponent<MeshFilter>().mesh, matrix, r.GetComponent<Renderer>().sharedMaterial, 0, Camera.main, 0, BlockOne, false, false);
        }

        for (int i = 0; i < BSector.Portal.Count; ++i)
        {
            Face g = Faces[BSector.Portal[i]];

            float d = PointDistanceToPlane(Planes[BSector.Portal[i]], CamPoint);

            List<Plane> PortalPlanes = new List<Plane>();

            if (d < -0.1f)
            {
                continue;
            }

            if (VisitedSector.Contains(Polyhedrons[g.Portal]) && d <= 0)
            {
                continue;
            }

            if (Sectors.Contains(Polyhedrons[g.Portal]))
            {
                for (int n = 0; n < APlanes.Count; n++)
                {
                    PortalPlanes.Add(APlanes[n]);
                }

                GetPortals(PortalPlanes, Polyhedrons[g.Portal]);

                continue;
            }

            if (d != 0)
            {
                List<Vector3> verticesout = ClippingPlanes(g.Vertices, APlanes);

                if (verticesout.Count > 2)
                {
                    CreateClippingPlanes(verticesout, PortalPlanes, CamPoint);

                    GetPortals(PortalPlanes, Polyhedrons[g.Portal]);
                }
            }
        }
    }
}
