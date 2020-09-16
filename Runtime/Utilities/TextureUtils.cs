using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System;

public class PingPongTexture
{
  bool swapState;
  RenderTextureDescriptor descr;

  public RenderTexture current
  {
    get
    {
      return swapState ? A : B;
    }
  }
  public RenderTexture old
  {
    get
    {
      return swapState ? B : A;
    }
  }
  RenderTexture A;
  RenderTexture B;

  public void Update(Vector3Int res, RenderTextureDescriptor d, bool existenceIsRequired)
  {
    descr = d;

    A = TextureUtil.UpdateTexture(A, res, descr, existenceIsRequired);
    B = TextureUtil.UpdateTexture(B, res, descr, existenceIsRequired);
  }
  public void Swap()
  {
    swapState = !swapState;
  }
};


public static class TextureUtil
{
  //Quickly written to load a specific file, not general.
  //Also...Unity lacks so many basic features....like 16-bit textures...also C# is really inefficient...etc...
  public static Texture2D ReadPPM(string resourcename)
  {
    TextAsset ta = Resources.Load(resourcename, typeof(TextAsset)) as TextAsset;
    byte[] bytes = ta.bytes;
    int index = 0;

    string id = "";
    string _width = "";
    string _height = "";
    string _maxval = "";
    for (; index < bytes.Length && bytes[index]!= 0x0A && bytes[index] != 0x20; index++) id += (char)bytes[index];
    index++;
    for (; index < bytes.Length && bytes[index]!= 0x0A && bytes[index] != 0x20; index++) _width += (char)bytes[index];
    index++;
    for (; index < bytes.Length && bytes[index]!= 0x0A && bytes[index] != 0x20; index++) _height += (char)bytes[index];
    index++;
    for (; index < bytes.Length && bytes[index]!= 0x0A && bytes[index] != 0x20; index++) _maxval += (char)bytes[index];
    index++;

    int width = int.Parse(_width);
    int height = int.Parse(_height);
    int maxval = int.Parse(_maxval);


    //Don't know an easy way to convert to 16-bit floats, will use 32-bit even though it's pointlessly inefficient
    //Have decided not to use image based noise anyway so for now this'll do.
    Texture2D img = new Texture2D(width, height, maxval < 256 ? GraphicsFormat.R8G8B8_UNorm : GraphicsFormat.R32G32B32_SFloat, TextureCreationFlags.None);

    float[] bytesShortened = new float[(bytes.Length - index)/2];
    for (int i = 0; i < bytesShortened.Length; i++)
    {
      bytesShortened[i] = (float)(bytes[i * 2 + index] * 256 + bytes[i * 2 + 1 + index])/(float)(65536);
    }

    // create a byte array and copy the floats into it...
    var byteArray = new byte[bytesShortened.Length * 4];
    Buffer.BlockCopy(bytesShortened, 0, byteArray, 0, byteArray.Length);

    img.LoadRawTextureData(byteArray);
    img.Apply();
    img.filterMode = FilterMode.Point;

    ///R16G16B16_UNorm should work but I get a platform not supporting Sample usage error
    /*
    Texture2D img = new Texture2D(width, height, maxval < 256 ? GraphicsFormat.R8G8B8_UNorm : GraphicsFormat.R16G16B16_UNorm, TextureCreationFlags.None);

    //Real efficient C#
    byte[] bytesShortened = new byte[bytes.Length - index];
    Array.Copy(bytes, 5, bytesShortened, 0, bytesShortened.Length);

    img.LoadRawTextureData(bytesShortened);
    img.Apply();*/

    return img;

  }

  public static Texture2D NoiseRGB16bittex = null;
  public static Texture2D NoiseRGB16bit
  {
    get
    {
      if (NoiseRGB16bittex == null)
        NoiseRGB16bittex = ReadPPM("VXGI/HDR_RGB_0");

      return NoiseRGB16bittex;
    }
  }

  public static Mesh MakeQuadMeshWhyDoesMeshNotHaveAnyUsefulConstructors()
  {
    Mesh mesh = new Mesh();
    Vector3[] verts = new Vector3[4]
    {
      new Vector3(-1,-1,0),
      new Vector3(1,-1,0),
      new Vector3(-1,1,0),
      new Vector3(1,1,0)
    };
    Vector2[] uvs = new Vector2[4]
    {
      new Vector2(0,1),
      new Vector2(1,1),
      new Vector2(0,0),
      new Vector2(1,0)
    };

    mesh.vertices = verts;
    mesh.uv = uvs;
    int[] tris = new int[6]
    {
      0, 2, 1,
      2, 3, 1
    };
    mesh.triangles = tris;

    return mesh;
  }
  static Mesh quadMesh;
  static Mesh quad {
    get
    {
      if (quadMesh == null)
      {
        quadMesh = MakeQuadMeshWhyDoesMeshNotHaveAnyUsefulConstructors();
      }
      return quadMesh;
    }
  }


  public static void Blit(CommandBuffer command, RenderTargetIdentifier source, RenderTargetBinding dest, Material mat, int pass)
  {
    command.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
    command.SetRenderTarget(dest);
    command.DrawMesh(quad, Matrix4x4.identity, mat, 0, pass);
  }




  public static void DisposeTexture(RenderTexture tex)
  {
    //I will never understand the appeal of managed languages that need things like this.
    if (tex != null)
    {
      if (tex.IsCreated()) tex.Release();
      MonoBehaviour.DestroyImmediate(tex);
    }
  }
  public static void DisposeBuffer(ComputeBuffer buf)
  {
    if (buf != null)
    {
      buf.Dispose();
    }
  }
  public static ComputeBuffer UpdateBuffer(ComputeBuffer buf, int count, int stride, ComputeBufferType type, bool existenceIsRequired)
  {
    if (!existenceIsRequired)
    {
      DisposeBuffer(buf);
      return null;
    }

    if (buf == null || buf.count != count || buf.stride != stride /*|| buf.type != type - can't check the type...*/)
    {
      //Debug.Log("Update buffer" + count.ToString() + ", " + stride.ToString());
      DisposeBuffer(buf);
      return new ComputeBuffer(count, stride, type);
    }

    return buf;
  }
  public static RenderTexture UpdateTexture(RenderTexture tex, Vector3Int res, RenderTextureDescriptor desc, bool existenceIsRequired)
  {
    if (!existenceIsRequired)
    {
      DisposeTexture(tex);
      return null;
    }

    desc.width = res.x;
    desc.height = res.y;
    desc.volumeDepth = res.z;
    //Please tell me there's an in-built way to do this.
    bool identical = tex != null
                     && tex.width == desc.width
                     && tex.height == desc.height
                     && tex.volumeDepth == desc.volumeDepth
                     && tex.graphicsFormat == desc.graphicsFormat
                     && tex.dimension == desc.dimension
                     && tex.enableRandomWrite == desc.enableRandomWrite
                     //&& tex.msaaSamples == desc.msaaSamples - can't check the samples...
                     && tex.sRGB == desc.sRGB
                    ;
    if (!identical)
    {
      //Debug.Log("Update texture" + desc.width.ToString()+ ", " + desc.height.ToString() + ", " + desc.volumeDepth.ToString() + ", ");
      DisposeTexture(tex);
      tex = new RenderTexture(desc);
      tex.filterMode = FilterMode.Point;
      tex.Create();
    }

    return tex;
  }
}