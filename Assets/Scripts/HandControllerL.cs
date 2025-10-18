using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class GloveReceiver : MonoBehaviour
{
    [Header("연결 설정")]
    [Tooltip("장치 관리자에서 확인한 COM 포트 번호")]
    public string portName = "COM8";

    [Header("3D 모델 관절 연결")]
    [Tooltip("손목 또는 손바닥에 해당하는 루트 오브젝트")]
    public Transform handRoot;
    [Tooltip("제어할 5개의 손가락 관절 (엄지부터 새끼 순서로)")]
    public Transform[] fingerJoints = new Transform[5];

    [Header("움직임 캘리브레이션")]
    [Tooltip("각 손가락별로 플렉스 센서가 펴졌을 때의 값 (엄지부터)")]
    public int[] flexMin = new int[5];
    [Tooltip("각 손가락별로 플렉스 센서가 완전히 굽혀졌을 때의 값 (엄지부터)")]
    public int[] flexMax = new int[5];
    [Tooltip("손가락이 최대로 굽혀질 각도")]
    public float maxFingerAngle = 90.0f;

    [Header("Quaternion 캘리브레이션")]
    [Tooltip("스페이스바를 눌러 현재 자세를 중립으로 설정")]
    public KeyCode calibrationKey = KeyCode.Space;
    [Tooltip("회전 스무딩 강도 (0~1)")]
    [Range(0f, 1f)]
    public float rotationSmoothing = 0.3f;
    
    // --- 내부 변수들 ---
    private SerialPort serialPort;
    private Thread dataReadThread;
    private bool isRunning = false;
    private string receivedString;
    private readonly object lockObject = new object();

    private int[] flexVals = new int[5];
    
    private Quaternion sensorQuat = Quaternion.identity;
    private Quaternion calibrationOffset = Quaternion.identity; // 캘리브레이션 오프셋
    private bool isCalibrated = false;

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, 115200) { ReadTimeout = 200 };
            serialPort.Open();
            isRunning = true;
            dataReadThread = new Thread(ReadDataThread);
            dataReadThread.IsBackground = true;
            dataReadThread.Start();
            Debug.Log($"✅ [왼손] Bluetooth-Serial 포트 연결 성공: {portName}");
            Debug.Log($"💡 [{calibrationKey}] 키를 눌러 Quaternion 캘리브레이션하세요.");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ [왼손] Bluetooth-Serial 포트 연결 실패: {e.Message}");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(calibrationKey))
        {
            Calibrate();
        }

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
        }

        UpdateHandModel();
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
            catch (TimeoutException) { /* 무시 */ }
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
            data = data.TrimStart(',');
            string[] parts = data.Split(',');
            if (parts.Length >= 12)
            {
                for (int i = 0; i < 5; i++)
                {
                    flexVals[i] = int.Parse(parts[i]);
                }

                float w = float.Parse(parts[8]);
                float x = float.Parse(parts[9]);
                float y = float.Parse(parts[10]);
                float z = float.Parse(parts[11]);
                
                sensorQuat = new Quaternion(x, y, -z, -w);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠ [왼손] 데이터 파싱 실패: {data} | 오류: {e.Message}");
        }
    }

    void Calibrate()
    {
        if (handRoot == null) return;
        
        // ✨ 핵심 수정: sensorRotationOffset 대신, 현재 손 모델의 실제 회전 값을 기준으로 삼습니다.
        // "현재 손 모델의 자세(handRoot.rotation)가 현재 센서 값(sensorQuat)의 기준이 되도록 오프셋을 설정하라"
        calibrationOffset = handRoot.rotation * Quaternion.Inverse(sensorQuat);
        isCalibrated = true;
        
        Debug.Log("✅ [왼손] Quaternion 캘리브레이션 완료! 현재 자세를 기준으로 설정합니다.");
    }

    private void UpdateHandModel()
    {
        if (handRoot == null) return;

        Quaternion finalQuat;

        if (isCalibrated)
        {
            // 캘리브레이션 후: 보정된 회전 값 적용
            finalQuat = calibrationOffset * sensorQuat;
        }
        else
        {
            // 캘리브레이션 전: 센서 값을 그대로 사용 (초기 자세 확인용)
            // handRoot의 초기 회전 값을 유지한 채로 센서 값을 더함
            finalQuat = handRoot.rotation * sensorQuat;
        }
        
        handRoot.rotation = Quaternion.Slerp(handRoot.rotation, finalQuat, rotationSmoothing);

        // 손가락 관절 업데이트
        for (int i = 0; i < fingerJoints.Length; i++)
        {
            if (fingerJoints[i] != null && i < flexMin.Length && i < flexMax.Length)
            {
                float bendAmount = Mathf.InverseLerp(flexMin[i], flexMax[i], flexVals[i]);
                float targetAngle = bendAmount * maxFingerAngle;
                Quaternion initialRotation;

                switch (i)
                {
                    case 0: // 엄지
                        initialRotation = Quaternion.Euler(-0.539f, -0.005f, 17.365f);
                        fingerJoints[i].localRotation = initialRotation * Quaternion.Euler(0f, 0f, targetAngle);
                        break;
                    case 1: // 검지
                        initialRotation = Quaternion.Euler(14.478f, 85.578f, 11.701f);
                        fingerJoints[i].localRotation = initialRotation * Quaternion.Euler(0f, 0f, targetAngle);
                        break;
                    case 2: // 중지
                        initialRotation = Quaternion.Euler(3.078f, 99.098f, 9.556f);
                        fingerJoints[i].localRotation = initialRotation * Quaternion.Euler(0f, 0f, targetAngle);
                        break;
                    case 3: // 약지
                        initialRotation = Quaternion.Euler(-7.489f, 109.398f, 6.735f);
                        fingerJoints[i].localRotation = initialRotation * Quaternion.Euler(0f, 0f, targetAngle);
                        break;
                    case 4: // 소지
                        initialRotation = Quaternion.Euler(-15.24f, 123.18f, 9.379f);
                        fingerJoints[i].localRotation = initialRotation * Quaternion.Euler(0f, 0f, targetAngle);
                        break;
                }
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