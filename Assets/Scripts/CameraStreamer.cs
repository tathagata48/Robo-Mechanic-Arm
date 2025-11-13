using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

namespace RoboMechanicArm
{
    /// <summary>
    /// Streams images from the attached camera to a remote python service and
    /// dispatches the commands returned by the service to a <see cref="VisionCommandRouter"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraStreamer : MonoBehaviour
    {
        [Header("Networking")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 5005;
        [Tooltip("Seconds between captured frames sent to python.")]
        [SerializeField] private float captureInterval = 0.25f;
        [Header("Command Routing")]
        [SerializeField] private VisionCommandRouter commandRouter;

        private Camera _camera;
        private TcpClient _client;
        private NetworkStream _stream;
        private Texture2D _captureTexture;
        private RenderTexture _renderTexture;
        private readonly byte[] _lengthBuffer = new byte[4];
        private bool _isConnected;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (commandRouter == null)
            {
                commandRouter = FindObjectOfType<VisionCommandRouter>();
            }
        }

        private void OnEnable()
        {
            StartCoroutine(CaptureLoop());
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private IEnumerator CaptureLoop()
        {
            // wait one frame to ensure camera has initialised
            yield return null;

            while (!_isConnected)
            {
                TryConnect();
                if (!_isConnected)
                {
                    yield return new WaitForSeconds(1f);
                }
            }

            while (_isConnected)
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    SendFrame();
                    ReadCommands();
                }
                catch (IOException)
                {
                    Debug.LogWarning("Lost connection to python vision service. Attempting to reconnect...");
                    Disconnect();
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                catch (SocketException ex)
                {
                    Debug.LogWarning($"Socket exception: {ex.Message}");
                    Disconnect();
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                yield return new WaitForSeconds(captureInterval);
            }
        }

        private void TryConnect()
        {
            try
            {
                _client = new TcpClient();
                _client.Connect(serverHost, serverPort);
                _stream = _client.GetStream();
                _stream.ReadTimeout = 100;
                _stream.WriteTimeout = 1000;
                _isConnected = true;
                Debug.Log("CameraStreamer connected to python vision server.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unable to connect to python server: {ex.Message}");
                _isConnected = false;
            }
        }

        private void Disconnect()
        {
            _isConnected = false;
            if (_stream != null)
            {
                _stream.Close();
                _stream = null;
            }
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
            if (_captureTexture != null)
            {
                Destroy(_captureTexture);
                _captureTexture = null;
            }
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        private void SendFrame()
        {
            if (_stream == null || !_stream.CanWrite)
            {
                throw new IOException("Network stream unavailable");
            }

            int width = _camera.pixelWidth;
            int height = _camera.pixelHeight;

            if (_renderTexture == null || _renderTexture.width != width || _renderTexture.height != height)
            {
                if (_renderTexture != null)
                {
                    _renderTexture.Release();
                    Destroy(_renderTexture);
                }

                _renderTexture = new RenderTexture(width, height, 24);
            }

            _camera.targetTexture = _renderTexture;
            _camera.Render();
            RenderTexture.active = _renderTexture;

            if (_captureTexture == null || _captureTexture.width != width || _captureTexture.height != height)
            {
                if (_captureTexture != null)
                {
                    Destroy(_captureTexture);
                }
                _captureTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            _captureTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            _captureTexture.Apply();

            _camera.targetTexture = null;
            RenderTexture.active = null;

            byte[] jpg = _captureTexture.EncodeToJPG(60);
            WriteLengthPrefixedBytes(jpg);
        }

        private void ReadCommands()
        {
            if (_stream == null || !_stream.CanRead)
            {
                return;
            }

            while (_client != null && _client.Connected && _stream.DataAvailable)
            {
                if (!TryReadExact(_lengthBuffer, 4))
                {
                    return;
                }

                int messageLength = BitConverter.ToInt32(_lengthBuffer, 0);
                if (messageLength <= 0)
                {
                    continue;
                }

                byte[] payload = new byte[messageLength];
                if (!TryReadExact(payload, messageLength))
                {
                    return;
                }

                string command = System.Text.Encoding.UTF8.GetString(payload).Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    commandRouter?.HandleCommand(command);
                }
            }
        }

        private bool TryReadExact(byte[] buffer, int expected)
        {
            int offset = 0;
            while (offset < expected)
            {
                try
                {
                    int read = _stream.Read(buffer, offset, expected - offset);
                    if (read <= 0)
                    {
                        return false;
                    }
                    offset += read;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
            return true;
        }

        private void WriteLengthPrefixedBytes(byte[] payload)
        {
            byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
            _stream.Write(lengthBytes, 0, lengthBytes.Length);
            _stream.Write(payload, 0, payload.Length);
        }
    }
}
