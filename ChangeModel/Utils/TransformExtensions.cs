//using UnityEngine;

//namespace Cavi.AppearanceMod.Utils
//{
//    public static class TransformExtensions
//    {
//        public static string GetPath(this Transform current)
//        {
//            if (current == null) return string.Empty;
//            string path = current.name;
//            while (current.parent != null)
//            {
//                current = current.parent;
//                path = current.parent.name + "/" + path;
//            }
//            return path;
//        }
//    }
//}