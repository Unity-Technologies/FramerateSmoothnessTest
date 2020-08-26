using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class SmoothnessTestScript : MonoBehaviour
{
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

    const int kMargin = 100;

    int m_FrameIndex;
    float m_LastUpdateTime;

    int m_DiscreteRegionWidth;
    int m_DiscretePositionCount;
    int m_DiscretePositionIndex;
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

        m_DiscretePositionIndex++;
        if (m_DiscretePositionIndex > m_DiscretePositionCount - 1)
        {
            Setup(currentResolution);
            return;
        }

        var perfectMovementCubePosition = m_PerfectMovementCubeTransform.position;
        perfectMovementCubePosition.x = kMargin + m_DiscretePositionIndex * m_DiscreteRegionWidth + 1;
        m_PerfectMovementCubeTransform.position = perfectMovementCubePosition;

        var deltaTimeMovement = m_CubeVelocity * Time.deltaTime;
        var deltaTimeCubePosition = m_DeltaTimeCubeTransform.position;
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
        if (velocity == Vector3.zero || velocity.x < 0)
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

        m_Camera.orthographic = true;
        m_Camera.orthographicSize = resolution.Height / 2.0f;
        m_Camera.transform.position = new Vector3(resolution.Width / 2.0f, resolution.Height / 2.0f, -1);

        m_FrameIndex = 0;
        m_DiscretePositionCount = resolution.RefreshRate.Numerator / resolution.RefreshRate.Denominator + 1;
        m_DiscretePositionIndex = 0;
        m_DiscreteRegionWidth = (resolution.Width - 2 * kMargin) / m_DiscretePositionCount;

        var integerRefreshRate = resolution.RefreshRate.Numerator / resolution.RefreshRate.Denominator;
        var floatRefreshRate = (float)resolution.RefreshRate.Numerator / resolution.RefreshRate.Denominator;
        m_CubeVelocity = floatRefreshRate * m_DiscreteRegionWidth * (m_DiscretePositionCount - 1) / integerRefreshRate;

        var linePositionX = kMargin;
        var linePositionY = resolution.Height / 2.0f;
        var lineCount = m_DiscretePositionCount + 1;
        for (int i = 0; i < lineCount; i++)
        {
            var line = AllocUnitCube();
            m_Lines.Add(line);

            var position = new Vector3(linePositionX, linePositionY);
            var scale = new Vector3(1.0f, -resolution.Height);
            SetupCube(line.transform, position, scale, m_LineMaterial);

            linePositionX += m_DiscreteRegionWidth;
        }

        var cubePosition = new Vector3(kMargin + m_DiscreteRegionWidth * m_DiscretePositionIndex + 1, 12 * resolution.Height / 15);
        var cubeScale = new Vector3(m_DiscreteRegionWidth - 1, m_DiscreteRegionWidth - 1);

        m_PerfectMovementCube = AllocUnitCube();
        m_PerfectMovementCubeTransform = m_PerfectMovementCube.transform;
        SetupCube(m_PerfectMovementCubeTransform, cubePosition, cubeScale, m_MovingSquareMaterial);

        m_DeltaTimeCube = AllocUnitCube();
        m_DeltaTimeCubeTransform = m_DeltaTimeCube.transform;

        cubePosition.y = 11 * resolution.Height / 15;
        SetupCube(m_DeltaTimeCubeTransform, cubePosition, cubeScale, m_MovingSquareMaterial);

        m_FixedTimeCube = AllocUnitCubeWithRigidbody();
        m_FixedTimeCubeRigidBody = m_FixedTimeCube.GetComponent<Rigidbody>();
        m_FixedTimeCubeRigidBody.useGravity = false;
        m_FixedTimeCubeRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        m_FixedTimeCubeRigidBody.velocity = Vector3.zero;

        cubePosition.y = 10 * resolution.Height / 15;
        SetupCube(m_FixedTimeCube.transform, cubePosition, cubeScale, m_MovingSquareMaterial);
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
        obj.SetActive(false);
        m_ObjectPool.Add(obj);
    }

    void ReleaseUnitCubeWithRigidBody(GameObject obj)
    {
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
