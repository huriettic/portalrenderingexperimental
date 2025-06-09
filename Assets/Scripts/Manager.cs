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

    public float speed = 7f;
    public float jumpHeight = 2f;
    public float gravity = 5f;
    public float sensitivity = 10f;
    public float clampAngle = 90f;
    public float smoothFactor = 25f;

    private Vector2 targetRotation;
    private Vector3 targetMovement;
    private Vector2 currentRotation;
    private Vector3 currentForce;

    public CharacterController Player;

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

    public List<List<Plane>> ListOfListPlanes = new List<List<Plane>>();

    public List<List<Vector3>> ListOfListVertices = new List<List<Vector3>>();

    public List<int> planetemp = new List<int>();

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
        PlayerInput();

        Sectors.Clear();

        GetPolyhedrons(CurrentSector);

        CamPlanes.Clear();

        ReadFrustumPlanes(Cam, CamPlanes);

        CamPlanes.RemoveAt(5);

        CamPlanes.RemoveAt(4);

        VisitedSector.Clear();

        rm = 0;

        ListOfListPlanes.Clear();

        ListOfListVertices.Clear();

        GetPortals(CamPlanes, CurrentSector);
    }

    void FixedUpdate()
    {
        if (!Player.isGrounded)
        {
            currentForce.y -= gravity * Time.deltaTime;
        }
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
                for (int b = 0; b < Polyhedrons[i].Render.Count; b++)
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

    public void PlayerInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.Space) && Player.isGrounded)
        {
            currentForce.y = jumpHeight;
        }

        float mousex = Input.GetAxisRaw("Mouse X");
        float mousey = Input.GetAxisRaw("Mouse Y");

        targetRotation.x -= mousey * sensitivity;
        targetRotation.y += mousex * sensitivity;

        targetRotation.x = Mathf.Clamp(targetRotation.x, -clampAngle, clampAngle);

        currentRotation = Vector2.Lerp(currentRotation, targetRotation, smoothFactor * Time.deltaTime);

        Cam.transform.localRotation = Quaternion.Euler(currentRotation.x, 0f, 0f);
        Player.transform.rotation = Quaternion.Euler(0f, currentRotation.y, 0f);

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        targetMovement = (Player.transform.right * horizontal + Player.transform.forward * vertical).normalized;

        Player.Move((targetMovement + currentForce) * speed * Time.deltaTime);
    }

    public List<Vector3> ClippingPlane(List<Vector3> invertices, Plane aPlane, float aEpsilon = 0.001f)
    {
        m_Dists.Clear();
        outvertices.Clear();
        
        int count = invertices.Count;
        if (m_Dists.Capacity < count)
            m_Dists.Capacity = count;
        if (outvertices.Capacity < count)
            outvertices.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = invertices[i];
            m_Dists.Add(aPlane.GetDistanceToPoint(p));
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

            ListOfListVertices.Add(invertices);
        }
        return invertices;
    }

    public bool CheckRadius(Polyhedron asector, Vector3 campoint)
    {
        bool PointIn = true;

        for (int e = 0; e < asector.Planes.Count; e++)
        {
            if (Planes[asector.Planes[e]].GetDistanceToPoint(campoint) < -0.6f)
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
            if (Planes[asector.Planes[i]].GetDistanceToPoint(campoint) < 0)
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

            float d = Planes[BSector.Render[i]].GetDistanceToPoint(CamPoint);

            if (d < -0.1f)
            {
                continue;
            }

            Triangles.Clear();

            UVs.Clear();

            List<Vector3> verticesout = ClippingPlanes(r, APlanes);

            ListOfListVertices.Add(verticesout);

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
                        UVs.Add(new Vector2(XPlane.GetDistanceToPoint(verticesout[e]) / 2.5f, YPlane.GetDistanceToPoint(verticesout[e]) / 2.5f));
                    }
                }
                else
                {
                    LeftPlane = new Plane((r[2] - r[1]).normalized, r[1]);
                    TopPlane = new Plane((r[1] - r[0]).normalized, r[1]);

                    List<Plane> planes = new List<Plane>()
                    {
                        LeftPlane, TopPlane
                    };

                    ListOfListPlanes.Add(planes);

                    for (int e = 0; e < verticesout.Count; e++)
                    {
                        UVs.Add(new Vector2(LeftPlane.GetDistanceToPoint(verticesout[e]) / 2.5f, TopPlane.GetDistanceToPoint(verticesout[e]) / 2.5f));
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

            float d = Planes[BSector.Portal[i]].GetDistanceToPoint(CamPoint);

            List<Plane> PortalPlanes = new List<Plane>();

            ListOfListPlanes.Add(PortalPlanes);

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

                ListOfListVertices.Add(verticesout);

                if (verticesout.Count > 2)
                {

                    CreateClippingPlanes(verticesout, PortalPlanes, CamPoint);

                    GetPortals(PortalPlanes, Polyhedrons[g.Portal]);
                }
            }
        }
    }
}
