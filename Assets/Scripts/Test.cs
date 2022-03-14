using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.VersionControl;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = System.Object;
using Task = System.Threading.Tasks.Task;

public class Test : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TextureData
    {
        public int    result;
        public int    length;
        public IntPtr data;
    }
    
#if UNITY_IOS
    private const string dll = "__Internal";
#else
    private const string dll = "astcenc";
#endif

    [DllImport(dll)]
    private static extern void Encode(string src,
                                      int blockSize,
                                      int imageQuality,
                                      int channels,
                                      int threadCount,
                                      out TextureData output);
    
    // Start is called before the first frame update
    async Task Start()
    {
        var img256  = "Images/palm-leaves-of-color-256";
        var img512  = "Images/palm-leaves-of-color-512";
        var img1024 = "Images/palm-leaves-of-color-1024";
        
        var tex256  = LoadImage_Texture2DCompress(img256);
        var tex512  = LoadImage_Texture2DCompress(img512);
        var tex1024 = LoadImage_Texture2DCompress(img1024);
        
        GetComponent<MeshRenderer>().material.SetTexture("_BaseMap", tex1024);

        var astc256 = LoadImage_AstcEncoder(img256, 256, 256);
        var astc512 = LoadImage_AstcEncoder(img512, 512, 512);
        var astc1024 = await LoadImage_AstcEncoder(img1024, 1024, 1024);

        GetComponent<MeshRenderer>().material.SetTexture("_BaseMap", astc1024);
    }

    Texture2D LoadImage_Texture2DCompress(string path)
    {
        var sw          = Stopwatch.StartNew();
        var sb          = new StringBuilder($"Texture2DCompress\nLoading {path}\n");
        var data        = Resources.Load<TextAsset>(path);
        sb.Append($"Loading data: {sw.Elapsed.TotalMilliseconds}MS\n");
        sw.Restart();
        var tex = new Texture2D(256, 256);
        tex.LoadImage(data.bytes);
        sb.Append($"Loading image: {sw.Elapsed.TotalMilliseconds}MS\n");
        sw.Restart();
        tex.Compress(false);
        sb.Append($"Compressing image: {sw.Elapsed.TotalMilliseconds}MS");
        Debug.Log(sb.ToString());
        return tex;
    }

    async Task<Texture2D> LoadImage_AstcEncoder(string path, int width, int height)
    {
        var sw  = Stopwatch.StartNew();
        var sb  = new StringBuilder($"AstcEncoder\nLoading {path}\n");
        var src = Path.Combine(Application.dataPath, "Resources", $"{path}.jpeg");
        
        try
        {
            var textureData = await EncodeInASeparateThread(src);
            sb.Append($"Encoding image: {sw.Elapsed.TotalSeconds}S\n");
            if (textureData.result != 0)
            {
                Debug.LogError($"Encoding failed! Error code: {textureData.result}");
                throw new Exception($"Encoding failed! Error code: {textureData.result}");
            }

            sw.Restart();
            var tex2D = new Texture2D(width, height, TextureFormat.ASTC_8x8, false);
            tex2D.LoadRawTextureData(textureData.data, textureData.length);
            sb.Append($"Loading texture data: {sw.Elapsed.TotalMilliseconds}MS\n");
            sw.Restart();
            tex2D.Apply(false);
            sb.Append($"Applying texture: {sw.Elapsed.TotalMilliseconds}MS\n");
            return tex2D;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
        finally
        {
            sw.Stop();
            Debug.Log(sb.ToString());
        }
    }

    Task<TextureData> EncodeInASeparateThread(string src)
    {
        var tcs = new TaskCompletionSource<TextureData>();
        // Queue the task.
        if (!ThreadPool.QueueUserWorkItem(ThreadProc, src))
        {
            tcs.SetException(new Exception("Failed to queue work item!"));
        }
        void ThreadProc(object path)
        {
            // No state object was passed to QueueUserWorkItem, so stateInfo is null.
            try
            {
                Encode((string)path, 8, 2, 4, 8, out var textureData);
                tcs.TrySetResult(textureData);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                tcs.TrySetException(e);
            }

        }

        return tcs.Task;
    }
}
