using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace Nuterra.BlockInjector
{
    public static class GameObjectJSON
    {
		// I dont think we need these given the Unity bundling
        public static Texture2D ImageFromFile(byte[] DATA)
        {
            Texture2D texture;
            texture = new Texture2D(2, 2);
            texture.LoadImage(DATA);
            return texture;
        }

        public static Texture2D CropImage(Texture2D source, Rect AreaNormalized)
        {
            int startX = Mathf.RoundToInt(source.width * AreaNormalized.x),
                startY = Mathf.RoundToInt(source.height * AreaNormalized.y),
                extentX = Mathf.RoundToInt(source.width * AreaNormalized.width),
                extentY = Mathf.RoundToInt(source.height * AreaNormalized.height);
            //int Sizeof = extentX * extentY;
            Texture2D Result = new Texture2D(extentX, extentY);
            Result.SetPixels(source.GetPixels(startX, startY, extentX, extentY));
            Result.Apply();
            return Result;
        }

        public static Texture2D ImageFromFile(string localPath)
        {
            string _localPath = System.IO.Path.Combine(Assembly.GetCallingAssembly().Location, "../" + localPath);
            byte[] data;
            if (System.IO.File.Exists(_localPath))
                data = System.IO.File.ReadAllBytes(_localPath);
            else if (System.IO.File.Exists(localPath))
                data = System.IO.File.ReadAllBytes(localPath);
            else throw new NullReferenceException("The file specified could not be found in " + localPath + " or " + _localPath);
            return ImageFromFile(data);
        }

        public static Sprite SpriteFromImage(Texture2D texture, float Scale = 1f)
        {
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(texture.width * 0.5f, texture.height * 0.5f), Mathf.Max(texture.width, texture.height) * Scale);
        }
		

        [Obsolete("Please use MeshFromData")]
        public static Mesh MeshFromFile(string FILEDATA, string name) => MeshFromData(FILEDATA);

        public static Mesh MeshFromData(string FILEDATA) => MeshFromData(FILEDATA, new Mesh());
        public static Mesh MeshFromData(string FILEDATA, Mesh modelToEdit)
        {
            return FastObjImporter.Instance.ImportFileFromData(FILEDATA, modelToEdit);
        }

        public static Mesh MeshFromFile(string localPath) => MeshFromFile(localPath, new Mesh());
        public static Mesh MeshFromFile(string localPath, Mesh modelToEdit)
        {
            string _localPath = System.IO.Path.Combine(Assembly.GetCallingAssembly().Location, "../" + localPath);
            if (!System.IO.File.Exists(_localPath))
            {
                if (System.IO.File.Exists(localPath))
                {
                    _localPath = localPath;
                }
                else throw new NullReferenceException("The file specified could not be found in " + localPath + " or " + _localPath);
            }
            return FastObjImporter.Instance.ImportFileFromPath(_localPath, modelToEdit);
        }
    }
}
