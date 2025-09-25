using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class GloveReceiver : MonoBehaviour
{
    [Header("연결 설정")]
    [Tooltip("장치 관리자에서 확인한 COM 포트 번호")]
    public string portName = "COM10"; // 사용자가 COM10으로 변경

    [Header("3D 모델 관절 연결")]
    [Tooltip("손목 또는 손바닥에 해당하는 루트 오브젝트")]
    public Transform handRoot;
    [Tooltip("제어할 5개의 손가락 관절 (엄지부터 새끼 순서로)")]
    public Transform[] fingerJoints = new Transform[5];

    [Header("캘리브레이션")]
    [Tooltip("각 손가락별로 플렉스 센서가 펴졌을 때의 값 (엄지부터)")]
    public int[] flexMin = new int[5];
    [Tooltip("각 손가락별로 플렉스 센서가 완전히 굽혀졌을 때의 값 (엄지부터)")]
    public int[] flexMax = new int[5];
    [Tooltip("손가락이 최대로 굽혀질 각도")]
    public float maxFingerAngle = 90.0f;

    [Header("손목 회전 보정")]
    [Tooltip("MPU-9250이 초기화될 때의 회전값을 보정합니다. (초기 기준점 설정)")]
    public Quaternion mpuCalibrationOffset = Quaternion.identity;
    [Tooltip("MPU-9250의 X, Y, Z축을 유니티 모델의 축에 맞추기 위한 최종 조정 (유니티 에디터에서 조절)")]
    public Vector3 finalRotationCorrection = new Vector3(0, 0, 0);

    [Header("손가락 회전 축 보정")]
    [Tooltip("각 손가락의 회전 축을 벡터로 설정합니다. (X, Y, Z)")]
    public Vector3[] fingerRotationAxes = new Vector3[5] {
        new Vector3(0, 0, 1), // 엄지 (Z축 회전)
        new Vector3(0, 0, 1), // 검지 (Z축 회전)
        new Vector3(0, 0, 1), // 중지 (Z축 회전)
        new Vector3(0, 0, 1), // 약지 (Z축 회전)
        new Vector3(0, 0, 1)  // 새끼 (Z축 회전)
    };

    // --- 내부 변수들 ---
    private SerialPort serialPort;
    private Thread dataReadThread;
    private bool isRunning = false;
    private string receivedString;
    private readonly object lockObject = new object();

    private int[] flexVals = new int[5];
    private float pitch, roll, yaw;

    private Quaternion initialMPURotation = Quaternion.identity;
    private bool isCalibrated = false;

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, 115200) { ReadTimeout = 200 };
            serialPort.Open();
            isRunning = true;
            dataReadThread = new Thread(ReadDataThread) { IsBackground = true };
            dataReadThread.Start();
            Debug.Log($"✅ [왼손] Bluetooth-Serial 포트 연결 성공: {portName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ [왼손] Bluetooth-Serial 포트 연결 실패: {e.Message}");
        }
    }

    void Update()
    {
        string dataToProcess = null;
        lock (lockObject)
        {
            if (receivedString != null)
            {
                dataToProcess = receivedString;
                receivedString = null;
            }
        }

        if (dataToProcess != null)
        {
            ParseData(dataToProcess);
            if (!isCalibrated)
            {
                initialMPURotation = Quaternion.Euler(pitch, yaw, roll);
                isCalibrated = true;
                Debug.Log($"✨ MPU 초기 캘리브레이션 완료: Pitch={pitch}, Yaw={yaw}, Roll={roll}");
            }
        }

        UpdateHandModel();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isCalibrated)
            {
                initialMPURotation = Quaternion.Euler(pitch, yaw, roll);
                Debug.Log($"✨ 손목 회전 재초기화 완료: Pitch={pitch}, Yaw={yaw}, Roll={roll}");
            }
        }
    }

    private void ReadDataThread()
    {
        while (isRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string line = serialPort.ReadLine();
                lock (lockObject)
                {
                    receivedString = line;
                }
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                if (isRunning) Debug.LogWarning($"⚠ [왼손] 데이터 수신 오류: {e.Message}");
            }
        }
    }

    private void ParseData(string data)
    {
        try
        {
            string[] parts = data.Split(',');
            if (parts.Length >= 11)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (i < parts.Length)
                    {
                        flexVals[i] = int.Parse(parts[i]);
                    }
                }

                pitch = float.Parse(parts[8]);
                roll = float.Parse(parts[9]);
                yaw = float.Parse(parts[10]);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠ [왼손] 데이터 파싱 실패: {data} | 오류: {e.Message}");
        }
    }

    private void UpdateHandModel()
    {
        if (!isCalibrated) return;

        // 1. 손목 회전 업데이트
        Quaternion currentMPURotation = Quaternion.Euler(yaw, pitch, roll);
        Quaternion deltaRotation = currentMPURotation * Quaternion.Inverse(initialMPURotation);

        // 최종 보정값 적용
        Quaternion adjustedRotation = Quaternion.Euler(finalRotationCorrection) * deltaRotation;
        handRoot.localRotation = adjustedRotation;

        // 2. 손가락 굽힘 업데이트
        for (int i = 0; i < fingerJoints.Length; i++)
        {
            if (fingerJoints[i] != null && i < flexMin.Length && i < flexMax.Length && i < fingerRotationAxes.Length)
            {
                float bendNormalized = Mathf.InverseLerp(flexMin[i], flexMax[i], flexVals[i]);
                float finalAngle = Mathf.Lerp(0, maxFingerAngle, bendNormalized);

                // 각 손가락의 회전 축을 변수로 사용 (모두 Z축으로 통일)
                // 손가락 굽힘을 Z축으로만 적용
                fingerJoints[i].localRotation = Quaternion.Euler(0f, 0f, finalAngle);
            }
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (dataReadThread != null && dataReadThread.IsAlive) dataReadThread.Join();
        if (serialPort != null && serialPort.IsOpen) serialPort.Close();
    }
}