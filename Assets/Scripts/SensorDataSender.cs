using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;
using System;

public class SensorDataSender : MonoBehaviour
{
    // HandController에서 접근하는 public 센서 값
    public int thumb, indexf, middle, ring, little;
    public float gx, gy, gz;

    [Serializable]
    public class SensorData
    {
        public int thumb;
        public int indexf;
        public int middle;
        public int ring;
        public int little;
        public float gx;
        public float gy;
        public float gz;
    }

    void Start()
    {
        StartCoroutine(SendSensorDataPeriodically());
    }

    IEnumerator SendSensorDataPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);
            yield return StartCoroutine(PostSensorDataToDjango());
        }
    }

    IEnumerator PostSensorDataToDjango()
    {
        string djangoUrl = "http://192.168.1.104:8000/sensor/upload/";  // 여긴 너 Django 주소로 바꿔줘

        // 클래스로 묶어서 전송
        SensorData data = new SensorData
        {
            thumb = this.thumb,
            indexf = this.indexf,
            middle = this.middle,
            ring = this.ring,
            little = this.little,
            gx = this.gx,
            gy = this.gy,
            gz = this.gz
        };

        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest req = new UnityWebRequest(djangoUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ 센서 데이터 Django 업로드 성공!");
            }
            else
            {
                Debug.LogError("❌ Django 업로드 실패: " + req.error);
            }
        }
    }
}
