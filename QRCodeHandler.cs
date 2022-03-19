using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using QRCodeDecoderLibrary;


public class QRCodeHandler : MonoBehaviour
{

    public ARCameraManager CameraManager;
    public ARCameraBackground CameraBackground;

    private Thread qrThread;

    private static QRDecoder codeReader = new QRDecoder();

    // Unity lifecycle goes (abridged):  Awake -> OnEnable -> Start -> Update -> OnDisable -> OnDestroy
    // We'll create & start the QR code analysis thread in OnEnable() and stop and destroy it in OnDisable()
    void OnEnable()
    {
        qrThread = new Thread(new ThreadStart(qrWorkerThread));
        qrThread.Name = "qrWorkerThread";
        qrThread.Start();
    }

    void OnDisable()
    {
        processInThread(new QRWorkerTask()
        {
            taskType = QRWorkerTaskType.Quit
        });
    }

    private void handleNewLocalSettings()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.frameCount % 20 == 0)
        {
#if UNITY_EDITOR
            StartCoroutine(analyseScreenshotSafe());
#elif UNITY_ANDROID || UNITY_IOS
            analyseCameraImageUnsafe();
            //analyseCameraImageBackground();
#endif
        }
    }

#if UNITY_IOSx
    private void OnPostRender()
    {
        if (Time.frameCount % 20 != 0)
        {
            return;
        }
        Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        //Read the pixels in the Rect starting at 0,0 and ending at the screen's width and height
        texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
        texture.Apply();
        Debug.Log($"OnPostRender");

        processInThread(new QRWorkerTask()
        {
            taskType = QRWorkerTaskType.AnalyzeImage,
            bmap = new BitmapWrapper(texture.GetRawTextureData<Color32>(), texture.width, texture.height),
        });
    }
#endif

    private IEnumerator analyseScreenshotSafe()
    {
        // Method 1: compatible, but very slow, introduces frame skips
        yield return new WaitForEndOfFrame();
        Texture2D result = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        result.Apply();

        processInThread(new QRWorkerTask()
        {
            taskType = QRWorkerTaskType.AnalyzeImage,
            bmap = new BitmapWrapper(result.GetPixels32(), result.width, result.height),
        });
    }

    private int qCount = 0;
    unsafe private void analyseCameraImageUnsafe()
    {
        // Method 2: supposedly native
        if (!CameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            return;
        }

        var sw = new System.Diagnostics.Stopwatch();

        using (image)
        {
            var squareSide = image.width < image.height ? image.width : image.height;
            while (squareSide > 1200)
            {
                squareSide /= 2;
            }
            var outputDimensions = new Vector2Int(squareSide, squareSide);

            // figure out what does it mean in the source, we want the middle rectangle. I.e. if 1280x720, we want the middle 720x720 px
            var px = (image.width - outputDimensions[0]) / 2;
            var py = (image.height - outputDimensions[1]) / 2;
            var inputRect = new RectInt(px, py, outputDimensions[0], outputDimensions[1]);

            //Debug.Log($"QR Scaling cpu image from {image.width}x{image.height} to {outputDimensions[0]}x{outputDimensions[1]}, inputRect: {inputRect}");

            var conversionParams = new XRCpuImage.ConversionParams
            {
                // Get the entire image.
                inputRect = inputRect,

                outputDimensions = outputDimensions,

                // Choose RGBA format.
                outputFormat = TextureFormat.RGBA32,

                // Flip
                transformation = XRCpuImage.Transformation.MirrorX
            };

            // See how many bytes you need to store the final image.
            int size = image.GetConvertedDataSize(conversionParams) + squareSide*4; // Apparently the zxing code overflows, so add a little bit extra memory
            var buffer = new Color32[size / 4]; // 4 bytes: RGBA

            // Extract the image data
            fixed (void* ptr = buffer)
            {
                sw.Restart();
                image.Convert(conversionParams, new IntPtr(ptr), size);
                sw.Stop();
                if (sw.ElapsedMilliseconds > 4)
                {
                    Debug.Log($"Convert() took {sw.ElapsedMilliseconds} ms");
                }
            }

            processInThread(new QRWorkerTask()
            {
                taskType = QRWorkerTaskType.AnalyzeImage,
                bmap = new BitmapWrapper(buffer, outputDimensions[0], outputDimensions[1]),
            });

            qCount++;
            //Debug.Log($"qCount: {qCount} image {outputDimensions[0]}x{outputDimensions[1]}");
        }
    }

    private void analyseCameraImageBackground()
    {

        var tx = CameraBackground.material.mainTexture;
        var tx2 = new Texture2D(tx.width, tx.height, tx.graphicsFormat, 0);
        Graphics.CopyTexture(tx, tx2);

        processInThread(new QRWorkerTask()
        {
            taskType = QRWorkerTaskType.AnalyzeImage,
            bmap = new BitmapWrapper(tx2.GetPixels32(), tx2.width, tx2.height),
        });
    }

    private void processInThread(QRWorkerTask t)
    {
        qrThreadTask = t;
        qrThreadTaskSema.Release();
    }

    public enum QRWorkerTaskType
    {
        AnalyzeImage,
        Quit
    }

    public class QRWorkerTask
    {
        public QRWorkerTaskType taskType;
        public BitmapWrapper bmap;
    }

    private static QRWorkerTask qrThreadTask;
    private static SemaphoreSlim qrThreadTaskSema = new SemaphoreSlim(0, 1);


    public enum QRDataFormat {
        ResultQRCode
    }

    public class QRData
    {
        public QRDataFormat format;
        public string text;
    }

    private static LockFreeQueue<QRData> qrResults = new LockFreeQueue<QRData>();
    private static QRCodeResult lastResult = null;

    public static bool DequeueResult(out QRData r)
    {
        return qrResults.Dequeue(out r);
    }

    private static void qrWorkerThread()
    {
        var count = 0;

        var sw = new System.Diagnostics.Stopwatch();

        while (true)
        {
            qrThreadTaskSema.Wait();
            if (qrThreadTask == null)
            {
                continue;
            }
            var task = qrThreadTask;
            qrThreadTask = null;

            count++;
            //Debug.Log($"################# qrWorkerThread dequeued a task {count}");

            if (task.taskType == QRWorkerTaskType.Quit)
            {
                break;
            }
            if (task.taskType != QRWorkerTaskType.AnalyzeImage)
            {
                Debug.LogError($"Invalid task type {task.taskType}");
                continue;
            }

            if (task.bmap == null)
            {
                Debug.LogError("task.bmap is null");
                continue;
            }
            task.bmap.ProcessPixels();

            QRCodeResult[] r = null;

            sw.Restart();
            try
            {
                r = codeReader.ImageDecoder(task.bmap);
                task.bmap = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in ZXing.Decode(): {e.Message}");
                continue;
            }
            sw.Stop();
            if (sw.ElapsedMilliseconds > 1)
            {
                Debug.Log($"Decode() took {sw.ElapsedMilliseconds} ms");
            }

            if (r == null || r.Length == 0)
            {
                continue;
            }

            if (lastResult != null && lastResult.DataString == r[0].DataString)
            {
                Debug.Log($"^^^^^^^^^^^^^^^^ [{Thread.CurrentThread.ManagedThreadId}] skipping/dup: {r[0].DataString}");
                continue;
            }
            else
            {
                Debug.Log($"################ [{Thread.CurrentThread.ManagedThreadId}] QR code detected: {r[0].DataString}");
            }
            lastResult = r[0];
            qrResults.Enqueue(new QRData()
            {
                format = QRDataFormat.ResultQRCode,
                text = lastResult.DataString,
            });
            task.bmap = null;
        }
    }
}
