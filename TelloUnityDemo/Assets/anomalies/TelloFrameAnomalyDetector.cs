using UnityEngine;
using System.IO;

public class TelloFrameAnomalyDetector : MonoBehaviour
{
    [Header("Tello Video Object")]
    public GameObject telloVideoObject;

    [Header("Detection Settings")]
    public float anomalyThreshold = 0.15f;

    [Header("Save Settings")]
    public string folderName = "DroneAnomalies";

    private Texture2D frameTexture;
    private float[] previousGray;

    private MeshRenderer videoRenderer;

    private Vector3 takeoffPosition;

    void Start()
    {
        videoRenderer = telloVideoObject.GetComponent<MeshRenderer>();

        // store takeoff reference position
        takeoffPosition = transform.position;

        string path = Path.Combine(Application.dataPath, folderName);

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        Debug.Log("Anomaly images saved at: " + path);
    }

    void Update()
    {
        Texture sourceTexture = videoRenderer.material.mainTexture;

        if (sourceTexture == null)
            return;

        CaptureFrame(sourceTexture);
    }

    void CaptureFrame(Texture source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);

        Graphics.Blit(source, rt);

        RenderTexture.active = rt;

        if (frameTexture == null)
            frameTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);

        frameTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        frameTexture.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        DetectAnomaly();
    }

    void DetectAnomaly()
    {
        Color[] pixels = frameTexture.GetPixels();

        float[] gray = new float[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            gray[i] = (pixels[i].r + pixels[i].g + pixels[i].b) / 3f;
        }

        if (previousGray != null)
        {
            float diff = 0;

            for (int i = 0; i < gray.Length; i += 20)
            {
                diff += Mathf.Abs(gray[i] - previousGray[i]);
            }

            diff /= (gray.Length / 20);

            if (diff > anomalyThreshold)
            {
                Debug.Log("ANOMALY DETECTED: " + diff);
                SaveFrame();
            }
        }

        previousGray = gray;
    }

    void SaveFrame()
    {
        string path = Path.Combine(Application.dataPath, folderName);

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // movement relative to takeoff
        Vector3 relativePos = transform.position - takeoffPosition;

        // convert meters to GPS-style offset
        double latOffset = relativePos.z / 111111.0;
        double lonOffset = relativePos.x / 111111.0;
        double altitude = relativePos.y;

        string fileName =
            timestamp +
            "_LAT" + latOffset.ToString("+0.000000;-0.000000") +
            "_LON" + lonOffset.ToString("+0.000000;-0.000000") +
            "_ALT" + altitude.ToString("F2") +
            ".png";

        byte[] bytes = frameTexture.EncodeToPNG();

        File.WriteAllBytes(Path.Combine(path, fileName), bytes);

        Debug.Log("Saved anomaly frame: " + fileName);
    }
}