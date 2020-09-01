using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
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

    [SerializeField]
    Color[] m_BackgroundCubeColors;

#pragma warning restore CS0649

    DisplayParameters m_Resolution;

    Material m_LineMaterial;
    Material m_MovingSquareMaterial;

    List<GameObject> m_ObjectPool;
    List<GameObject> m_RigidbodyObjectPool;

    GameObject m_LeftLine;
    GameObject m_RightLine;

    Transform m_CameraTransform;
    Transform m_PerfectMovementCubeTransform;
    Transform m_DeltaTimeCubeTransform;
    Rigidbody m_FixedTimeCubeRigidBody;

    Transform[] m_BackgroundCubes;
    Material[] m_BackgroundMaterials;

    int m_FrameIndex;
    float m_LastUpdateTime;

    int m_CubeWidth;
    float m_CubeVelocity;

    int m_NextBackgroundIndex;

    void Awake()
    {
        QualitySettings.vSyncCount = 1;

        m_ObjectPool = new List<GameObject>();
        m_RigidbodyObjectPool = new List<GameObject>();

        m_LineMaterial = Instantiate(m_MaterialPrefab);
        m_LineMaterial.color = m_LineColor;

        m_MovingSquareMaterial = Instantiate(m_MaterialPrefab);
        m_MovingSquareMaterial.color = m_MovingSquareColor;

        var backgroundCubeColorCount = m_BackgroundCubeColors.Length;
        m_BackgroundCubes = new Transform[2 * backgroundCubeColorCount];

        m_BackgroundMaterials = new Material[backgroundCubeColorCount];
        for (int i = 0; i < backgroundCubeColorCount; i++)
        {
            var material = Instantiate(m_MaterialPrefab);
            material.color = m_BackgroundCubeColors[i];
            m_BackgroundMaterials[i] = material;
        }

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

        var backgroundCubePosition = m_BackgroundCubes[2 * m_NextBackgroundIndex].position;
        var backgroundPositionX = backgroundCubePosition.x;
        if (m_CameraTransform.position.x - currentResolution.Width / 2 > backgroundPositionX)
        {
            backgroundCubePosition.x += currentResolution.Width;
            m_BackgroundCubes[2 * m_NextBackgroundIndex].position = backgroundCubePosition;

            backgroundCubePosition = m_BackgroundCubes[2 * m_NextBackgroundIndex + 1].position;
            backgroundCubePosition.x += currentResolution.Width;
            m_BackgroundCubes[2 * m_NextBackgroundIndex + 1].position = backgroundCubePosition;

            m_NextBackgroundIndex = (m_NextBackgroundIndex + 1) % m_BackgroundMaterials.Length;
        }
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

    private void Setup(in DisplayParameters resolution)
    {
        Cleanup();

        m_Resolution = resolution;

        m_FrameIndex = 0;

        var floatRefreshRate = (float)resolution.RefreshRate.Numerator / resolution.RefreshRate.Denominator;
        m_CubeWidth = (int)(2.0f * resolution.Height / floatRefreshRate);
        m_CubeVelocity = floatRefreshRate * m_CubeWidth;

        var cubePosition = new Vector3(resolution.Width / 2 - m_CubeWidth / 2, (resolution.Height - m_CubeWidth) / 2 + 2 * m_CubeWidth, 50.0f);
        var cubeScale = new Vector3(m_CubeWidth, m_CubeWidth, 1.0f);

        var perfectMovementCube = AllocUnitCube();
        perfectMovementCube.name = "Perfect movement cube";
        m_PerfectMovementCubeTransform = perfectMovementCube.transform;
        SetupCube(m_PerfectMovementCubeTransform, cubePosition, cubeScale, m_MovingSquareMaterial);

        var lineScale = new Vector3(1.0f, resolution.Height, 1.0f);

        m_LeftLine = AllocUnitCube();
        m_LeftLine.name = "Left line";
        Vector3 linePosition = new Vector3(cubePosition.x - 1, 0.0f, cubePosition.z + 10.0f); // Behind the cube, so that if cube intersects it, it's clearly visible
        SetupCube(m_LeftLine.transform, linePosition, lineScale, m_LineMaterial);
        m_LeftLine.transform.parent = m_PerfectMovementCubeTransform;

        m_RightLine = AllocUnitCube();
        m_RightLine.name = "Right line";
        linePosition.x = cubePosition.x + m_CubeWidth;
        SetupCube(m_RightLine.transform, linePosition, lineScale, m_LineMaterial);
        m_RightLine.transform.parent = m_PerfectMovementCubeTransform;

        var deltaTimeCube = AllocUnitCube();
        deltaTimeCube.name = "Delta time cube";
        m_DeltaTimeCubeTransform = deltaTimeCube.transform;

        cubePosition.y = (resolution.Height - m_CubeWidth) / 2;
        SetupCube(m_DeltaTimeCubeTransform, cubePosition, cubeScale, m_MovingSquareMaterial);

        var fixedTimeCube = AllocUnitCubeWithRigidbody();
        fixedTimeCube.name = "Fixed time cube";
        m_FixedTimeCubeRigidBody = fixedTimeCube.GetComponent<Rigidbody>();
        m_FixedTimeCubeRigidBody.useGravity = false;
        m_FixedTimeCubeRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        m_FixedTimeCubeRigidBody.velocity = Vector3.zero;

        cubePosition.y = (resolution.Height - m_CubeWidth) / 2 - 2 * m_CubeWidth;
        SetupCube(fixedTimeCube.transform, cubePosition, cubeScale, m_MovingSquareMaterial);

        m_Camera.orthographic = true;
        m_Camera.orthographicSize = resolution.Height / 2.0f;
        m_Camera.nearClipPlane = 1.0f;
        m_Camera.farClipPlane = 1000.0f;

        m_CameraTransform = m_Camera.transform;
        m_CameraTransform.position = new Vector3(resolution.Width / 2.0f, resolution.Height / 2.0f, 0.0f);
        m_CameraTransform.parent = m_PerfectMovementCubeTransform;

        m_NextBackgroundIndex = 0;

        int backgroundCubeColorCount = m_BackgroundCubeColors.Length;
        var backgroundPositionZ = cubePosition.z + 20.0f; // Behind vertical lines
        var backgroundCubeScale = new Vector3(m_CubeWidth / 2, m_CubeWidth / 2, 1.0f);

        for (int i = 0; i < backgroundCubeColorCount; i++)
        {
            var backgroundMaterial = m_BackgroundMaterials[i];
            var positionX = (i + 1) * (float)resolution.Width / backgroundCubeColorCount;

            var upperCube = AllocUnitCube();
            upperCube.name = "Background Cube";

            var upperPositionY = (resolution.Height - m_CubeWidth) / 2 + 5 * m_CubeWidth / 4;
            var upperPosition = new Vector3(positionX, upperPositionY, backgroundPositionZ);
            var upperCubeTransform = m_BackgroundCubes[2 * i] = upperCube.transform;
            SetupCube(upperCubeTransform, upperPosition, backgroundCubeScale, backgroundMaterial);

            var lowerCube = AllocUnitCube();
            lowerCube.name = "Background Cube";

            var lowerPositionY = (resolution.Height - m_CubeWidth) / 2 - 3 * m_CubeWidth / 4;
            var lowerPosition = new Vector3(positionX, lowerPositionY, backgroundPositionZ);
            var lowerCubeTransform = m_BackgroundCubes[2 * i + 1] = lowerCube.transform;
            SetupCube(lowerCubeTransform, lowerPosition, backgroundCubeScale, backgroundMaterial);
        }
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
        if (m_LeftLine)
        {
            ReleaseUnitCube(m_LeftLine);
            m_LeftLine = null;
        }

        if (m_RightLine)
        {
            ReleaseUnitCube(m_RightLine);
            m_RightLine = null;
        }

        if (m_PerfectMovementCubeTransform)
        {
            ReleaseUnitCube(m_PerfectMovementCubeTransform.gameObject);
            m_PerfectMovementCubeTransform = null;
        }

        if (m_DeltaTimeCubeTransform)
        {
            ReleaseUnitCube(m_DeltaTimeCubeTransform.gameObject);
            m_DeltaTimeCubeTransform = null;
        }

        if (m_FixedTimeCubeRigidBody)
        {
            ReleaseUnitCubeWithRigidBody(m_FixedTimeCubeRigidBody.gameObject);
            m_FixedTimeCubeRigidBody = null;
        }

        var backgroundCubeCount = m_BackgroundCubes.Length;
        for (int i = 0; i < backgroundCubeCount; i++)
        {
            if (m_BackgroundCubes[i])
            {
                ReleaseUnitCube(m_BackgroundCubes[i].gameObject);
                m_BackgroundCubes[i] = null;
            }
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
