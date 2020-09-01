using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class SmoothnessTestScript : MonoBehaviour
{
#pragma warning disable CS0649

    [SerializeField]
    Camera m_Camera;

    [SerializeField]
    GameObject m_UnitCube;

    [SerializeField]
    Material m_MaterialPrefab;

    [SerializeField]
    Color m_LineColor;

    [SerializeField]
    Color m_MovingSquareColor;

#pragma warning restore CS0649

    DisplayParameters m_Resolution;

    Material m_LineMaterial;
    Material m_MovingSquareMaterial;

    List<GameObject> m_ObjectPool;
    List<GameObject> m_RigidbodyObjectPool;

    List<GameObject> m_Lines;
    GameObject m_PerfectMovementCube;
    Transform m_PerfectMovementCubeTransform;

    GameObject m_DeltaTimeCube;
    Transform m_DeltaTimeCubeTransform;

    GameObject m_FixedTimeCube;
    Rigidbody m_FixedTimeCubeRigidBody;

    int m_FrameIndex;
    float m_LastUpdateTime;

    int m_CubeWidth;
    float m_CubeVelocity;

    void Awake()
    {
        QualitySettings.vSyncCount = 1;

        m_ObjectPool = new List<GameObject>();
        m_RigidbodyObjectPool = new List<GameObject>();
        m_Lines = new List<GameObject>();

        m_LineMaterial = Instantiate(m_MaterialPrefab);
        m_LineMaterial.color = m_LineColor;

        m_MovingSquareMaterial = Instantiate(m_MaterialPrefab);
        m_MovingSquareMaterial.color = m_MovingSquareColor;

        Setup(GetCurrentResolution());
    }

    void Update()
    {
        var currentResolution = GetCurrentResolution();
        if (Input.GetKeyDown(KeyCode.Space) || currentResolution != m_Resolution)
            Setup(currentResolution);

        m_LastUpdateTime = Time.time;
        m_FrameIndex++;
        if (m_FrameIndex < 10)
            return;

        if (m_FrameIndex > 10000) // Don't let floating point precision get out of wack.
        {
            Setup(currentResolution);
            return;
        }

        var perfectMovementCubePosition = m_PerfectMovementCubeTransform.position;
        var deltaTimeCubePosition = m_DeltaTimeCubeTransform.position;
        if (Mathf.Abs(perfectMovementCubePosition.x - deltaTimeCubePosition.x) > 3 * m_CubeWidth / 4)
        {
            // DeltaTime cube is out of bounds of by more than 75% of the frame. We likely dropped a frame - resync them.
            Setup(currentResolution);
            return;
        }

        perfectMovementCubePosition.x += m_CubeWidth;
        m_PerfectMovementCubeTransform.position = perfectMovementCubePosition;

        var deltaTimeMovement = m_CubeVelocity * Time.deltaTime;
        deltaTimeCubePosition.x += deltaTimeMovement;
        m_DeltaTimeCubeTransform.position = deltaTimeCubePosition;
    }

    private void FixedUpdate()
    {
        MoveCubeFixed(m_FixedTimeCubeRigidBody);
    }

    private void MoveCubeFixed(Rigidbody cube)
    {
        if (m_FrameIndex < 10)
            return;

        var velocity = cube.velocity;
        if (velocity == Vector3.zero)
        {
            var position = cube.position;
            var timeDiff = Time.fixedTime - m_LastUpdateTime;

            cube.velocity = velocity = new Vector3(m_CubeVelocity, 0, 0);
            position.x = m_DeltaTimeCubeTransform.position.x + velocity.x * timeDiff;
            cube.MovePosition(position);
        }
    }

    private void Setup(DisplayParameters resolution)
    {
        Cleanup();

        m_Resolution = resolution;

        m_FrameIndex = 0;
        m_CubeWidth = resolution.Height / 48;

        var floatRefreshRate = (float)resolution.RefreshRate.Numerator / resolution.RefreshRate.Denominator;
        m_CubeVelocity = floatRefreshRate * m_CubeWidth;

        var cubePosition = new Vector3(resolution.Width / 2 - m_CubeWidth / 2, (resolution.Height - m_CubeWidth) / 2 + 2 * m_CubeWidth, 50.0f);
        var cubeScale = new Vector3(m_CubeWidth, m_CubeWidth, 1.0f);

        m_PerfectMovementCube = AllocUnitCube();
        m_PerfectMovementCube.name = "Perfect movement cube";
        m_PerfectMovementCubeTransform = m_PerfectMovementCube.transform;
        SetupCube(m_PerfectMovementCubeTransform, cubePosition, cubeScale, m_MovingSquareMaterial);

        var lineScale = new Vector3(1.0f, resolution.Height, 1.0f);

        var leftLine = AllocUnitCube();
        leftLine.name = "Left line";
        Vector3 linePosition = new Vector3(cubePosition.x - 1, 0.0f, cubePosition.z + 10.0f); // Behind the cube, so that if cube intersects it, it's clearly visible
        SetupCube(leftLine.transform, linePosition, lineScale, m_LineMaterial);
        leftLine.transform.parent = m_PerfectMovementCubeTransform;
        m_Lines.Add(leftLine);

        var rightLine = AllocUnitCube();
        rightLine.name = "Right line";
        linePosition.x = cubePosition.x + m_CubeWidth;
        SetupCube(rightLine.transform, linePosition, lineScale, m_LineMaterial);
        rightLine.transform.parent = m_PerfectMovementCubeTransform;
        m_Lines.Add(rightLine);

        m_DeltaTimeCube = AllocUnitCube();
        m_DeltaTimeCube.name = "Delta time cube";
        m_DeltaTimeCubeTransform = m_DeltaTimeCube.transform;

        cubePosition.y = (resolution.Height - m_CubeWidth) / 2;
        SetupCube(m_DeltaTimeCubeTransform, cubePosition, cubeScale, m_MovingSquareMaterial);

        m_FixedTimeCube = AllocUnitCubeWithRigidbody();
        m_FixedTimeCube.name = "Fixed time cube";
        m_FixedTimeCubeRigidBody = m_FixedTimeCube.GetComponent<Rigidbody>();
        m_FixedTimeCubeRigidBody.useGravity = false;
        m_FixedTimeCubeRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        m_FixedTimeCubeRigidBody.velocity = Vector3.zero;

        cubePosition.y = (resolution.Height - m_CubeWidth) / 2 - 2 * m_CubeWidth;
        SetupCube(m_FixedTimeCube.transform, cubePosition, cubeScale, m_MovingSquareMaterial);

        m_Camera.orthographic = true;
        m_Camera.orthographicSize = resolution.Height / 2.0f;
        m_Camera.nearClipPlane = 1.0f;
        m_Camera.farClipPlane = 1000.0f;

        var cameraTransform = m_Camera.transform;
        cameraTransform.position = new Vector3(resolution.Width / 2.0f, resolution.Height / 2.0f, 0.0f);
        cameraTransform.parent = m_PerfectMovementCubeTransform;
    }

    private static void SetupCube(Transform cubeTransform, Vector3 position, Vector3 scale, Material material)
    {
        cubeTransform.position = position;
        cubeTransform.localScale = scale;

        var lineObject = cubeTransform.GetChild(0);
        var lineMeshRenderer = lineObject.GetComponent<MeshRenderer>();
        lineMeshRenderer.material = material;
    }

    private void Cleanup()
    {
        int count = m_Lines.Count;
        for (int i = 0; i < count; i++)
            ReleaseUnitCube(m_Lines[i]);

        m_Lines.Clear();

        if (m_PerfectMovementCube)
        {
            ReleaseUnitCube(m_PerfectMovementCube);
            m_PerfectMovementCube = null;
            m_PerfectMovementCubeTransform = null;
        }

        if (m_DeltaTimeCube)
        {
            ReleaseUnitCube(m_DeltaTimeCube);
            m_DeltaTimeCube = null;
            m_DeltaTimeCubeTransform = null;
        }

        if (m_FixedTimeCube)
        {
            ReleaseUnitCubeWithRigidBody(m_FixedTimeCube);
            m_FixedTimeCube = null;
            m_FixedTimeCubeRigidBody = null;
        }
    }

    GameObject AllocUnitCube()
    {
        if (m_ObjectPool.Count == 0)
            return Instantiate(m_UnitCube);

        var result = m_ObjectPool[m_ObjectPool.Count - 1];
        m_ObjectPool.RemoveAt(m_ObjectPool.Count - 1);

        result.SetActive(true);
        return result;
    }
    
    GameObject AllocUnitCubeWithRigidbody()
    {
        if (m_RigidbodyObjectPool.Count == 0)
        {
            var newResult = Instantiate(m_UnitCube);
            newResult.AddComponent<Rigidbody>();
            return newResult;
        }

        var result = m_RigidbodyObjectPool[m_RigidbodyObjectPool.Count - 1];
        m_RigidbodyObjectPool.RemoveAt(m_RigidbodyObjectPool.Count - 1);

        result.SetActive(true);
        return result;
    }

    void ReleaseUnitCube(GameObject obj)
    {
        obj.transform.parent = null;
        obj.SetActive(false);
        m_ObjectPool.Add(obj);
    }

    void ReleaseUnitCubeWithRigidBody(GameObject obj)
    {
        obj.transform.parent = null;
        obj.SetActive(false);
        m_RigidbodyObjectPool.Add(obj);
    }

#if UNITY_STANDALONE_WIN
    [DllImport("RefreshRateHelper")]
    static extern int GetCurrentRefreshRate(out int numerator, out int denominator);
#endif

    static DisplayParameters GetCurrentResolution()
    {
#if UNITY_STANDALONE_WIN
        int numerator, denominator;
        var hr = GetCurrentRefreshRate(out numerator, out denominator);
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);

        return new DisplayParameters(Screen.width, Screen.height, new Ratio(numerator, denominator));
#else
        var currentResolution = Screen.currentResolution;
        return new DisplayParameters(Screen.width, Screen.height, new Ratio(currentResolution.refreshRate, 1));
#endif
    }
}
