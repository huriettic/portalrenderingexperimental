using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class Manager : MonoBehaviour
{
    public string Name;

    private int rm;

    private Vector3 A;
    private Vector3 B;
    private Vector3 C;

    private int a;
    private int b;
    private int c;

    private Plane XPlane;
    private Plane YPlane;

    private Plane TopPlane;
    private Plane LeftPlane;

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

    public List<Mesh> RenderMeshes = new List<Mesh>();

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

    public List<Vector3> verticesout = new List<Vector3>();

    public List<int> Triangles = new List<int>();

    public List<Vector2> UVs = new List<Vector2>();

    public List<Vector3> outvertices = new List<Vector3>();

    public List<float> m_Dists = new List<float>();

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

        MakeMeshes();

        CreatePolygonPlane();
    }

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        A = new Vector3(0, 0, 0);
        B = new Vector3(-1, 0, 0);
        C = new Vector3(0, 0, -1);

        XPlane = new Plane((C - A).normalized, A);

        YPlane = new Plane((B - A).normalized, A);

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

        rm = 0;

        GetPortals(CamPlanes, CurrentSector);
    }

    public void Load()
    {
        string LoadLevel = Resources.Load<TextAsset>(Name).text;

        JsonUtility.FromJsonOverwrite(LoadLevel, this);
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

    public void MakeMeshes()
    {
        for (int i = 0; i < Polyhedrons.Count; i++)
        {
            for (int e = 0; e < Polyhedrons[i].Portal.Count; e++)
            {
                for (int b = 0; b < Polyhedrons[e].Render.Count; b++)
                {
                    RenderMeshes.Add(new Mesh());
                }
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
        outvertices.Clear();
        m_Dists.Clear();

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

            if (Sectors.Contains(Polyhedrons[f.Portal]))
            {
                continue;
            }

            bool r = CheckRadius(Polyhedrons[f.Portal], CamPoint);

            if (r == true)
            {
                GetPolyhedrons(Polyhedrons[f.Portal]);

                continue;
            }
        }

        bool t = CheckPolyhedron(ASector, CamPoint);

        if (t == true)
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

        for (int i = 0; i < BSector.Render.Count; i++)
        {
            List<Vector3> r = Faces[BSector.Render[i]].Vertices;

            GameObject h = LevelMeshes[Faces[BSector.Render[i]].FaceMesh];

            float d = PointDistanceToPlane(Planes[BSector.Render[i]], CamPoint);

            if (d < -0.1f)
            {
                continue;
            }

            Triangles.Clear();

            UVs.Clear();

            verticesout = ClippingPlanes(r, APlanes);

            if (verticesout.Count > 2)
            {
                for (int e = 2; e < verticesout.Count; e++)
                {
                    a = 0;
                    b = e - 1;
                    c = e;

                    Triangles.Add(a);
                    Triangles.Add(b);
                    Triangles.Add(c);
                }

                if (Planes[BSector.Render[i]].normal.y == 1 || Planes[BSector.Render[i]].normal.y == -1 || Planes[BSector.Render[i]].normal.x == 0 &&
                    Planes[BSector.Render[i]].normal.y == 0 && Planes[BSector.Render[i]].normal.z == 0)
                {
                    for (int e = 0; e < verticesout.Count; e++)
                    {
                        UVs.Add(new Vector2(PointDistanceToPlane(XPlane, verticesout[e]) / 2.5f, PointDistanceToPlane(YPlane, verticesout[e]) / 2.5f));
                    }
                }
                else
                {
                    LeftPlane = new Plane((r[2] - r[1]).normalized, r[1]);
                    TopPlane = new Plane((r[1] - r[0]).normalized, r[1]);

                    for (int e = 0; e < verticesout.Count; e++)
                    {
                        UVs.Add(new Vector2(PointDistanceToPlane(LeftPlane, verticesout[e]) / 2.5f, PointDistanceToPlane(TopPlane, verticesout[e]) / 2.5f));
                    }
                }

                Mesh mesh = RenderMeshes[rm];

                Material material = h.GetComponent<Renderer>().sharedMaterial;

                mesh.Clear();

                mesh.SetVertices(verticesout);
                mesh.SetUVs(0, UVs);
                mesh.SetTriangles(Triangles, 0, true);
                mesh.RecalculateNormals();

                Matrix4x4 matrix = Matrix4x4.TRS(h.transform.position, h.transform.transform.rotation, h.transform.transform.lossyScale);

                RenderParams rp = new RenderParams(material);

                Graphics.RenderMesh(rp, mesh, 0, matrix);

                rm++;
            }
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
                verticesout = ClippingPlanes(g.Vertices, APlanes);

                if (verticesout.Count > 2)
                {

                    CreateClippingPlanes(verticesout, PortalPlanes, CamPoint);

                    GetPortals(PortalPlanes, Polyhedrons[g.Portal]);
                }
            }
        }
    }
}
