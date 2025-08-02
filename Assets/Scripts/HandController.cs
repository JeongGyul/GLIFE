using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class HandController : MonoBehaviour
{
    public Transform[] fingers;  // 0: thumb, 1: index, 2: middle, 3: ring, 4: little
    public Transform handRoot;

    private int[] flexVals = new int[5];
    private float pitch, roll, yaw;

    private TcpListener listener;
    private Thread serverThread;
    private bool isRunning = false;

    public SensorDataSender sender; // SensorDataSender 연결

    void Start()
    {
        StartTCPServer();
    }

    void Update()
    {
        // 손가락 회전
        for (int i = 0; i < fingers.Length; i++)
        {
            float angle = Mathf.InverseLerp(800, 2000, flexVals[i]) * 90f;
            fingers[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        // 손 전체 회전
        handRoot.localRotation = Quaternion.Euler(pitch, yaw, -roll);
    }

    void StartTCPServer()
    {
        isRunning = true;
        serverThread = new Thread(() =>
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 8000);
                listener.Start();
                Debug.Log("✅ TCP 서버 시작됨 (포트 8000)");

                while (isRunning)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Debug.Log("✅ ESP32 연결됨");

                    using (NetworkStream stream = client.GetStream())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        try
                        {
                            while (client.Connected && isRunning)
                            {
                                string line = reader.ReadLine();
                                if (string.IsNullOrEmpty(line)) continue;

                                try
                                {
                                    string[] parts = line.Split(',');
                                    if (parts.Length >= 8)
                                    {
                                        for (int i = 0; i < 5; i++)
                                            flexVals[i] = int.Parse(parts[i]);

                                        pitch = float.Parse(parts[5]);
                                        roll = float.Parse(parts[6]);
                                        yaw = float.Parse(parts[7]);

                                        // SensorDataSender에 값 전달
                                        if (sender != null)
                                        {
                                            sender.thumb = flexVals[0];
                                            sender.indexf = flexVals[1];
                                            sender.middle = flexVals[2];
                                            sender.ring = flexVals[3];
                                            sender.little = flexVals[4];
                                            sender.gx = pitch;
                                            sender.gy = roll;
                                            sender.gz = yaw;
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning("⚠ 데이터 파싱 실패: " + line);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError("❌ 데이터 파싱 오류: " + ex.Message);
                                }
                            }
                        }
                        catch (IOException ioex)
                        {
                            Debug.LogWarning("⚠ 클라이언트 연결 중단됨: " + ioex.Message);
                        }
                        catch (ObjectDisposedException)
                        {
                            Debug.LogWarning("⚠ 서버 종료 중 스트림 접근됨 (무시 가능)");
                        }
                    }

                    Debug.Log("ℹ 클라이언트 연결 종료됨");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("❌ 서버 오류: " + e.Message);
            }
        });

        serverThread.IsBackground = true;
        serverThread.Start();
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        try
        {
            listener?.Stop();
            serverThread?.Join(); // 안전하게 종료
        }
        catch (Exception e)
        {
            Debug.LogWarning("⚠ 종료 중 예외: " + e.Message);
        }
    }
}
