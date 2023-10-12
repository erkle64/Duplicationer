//using System;
//using System.Linq;
//using System.Xml.Linq;
//using UnityEngine;

//namespace Unfoundry
//{
//    public class AssetBundleProxy
//    {
//        private delegate IntPtr returnMainAssetDelegate(IntPtr bundle);
//        private delegate IntPtr UnloadAllAssetBundlesDelegate(bool unloadAllObjects);
//        private delegate IntPtr GetAllLoadedAssetBundles_NativeDelegate();
//        private delegate IntPtr LoadFromFile_InternalDelegate(IntPtr path, uint crc, ulong offset);
//        private delegate IntPtr LoadFromMemory_InternalDelegate(IntPtr binary, uint crc);
//        private delegate IntPtr LoadFromStreamInternalDelegate(IntPtr stream, uint crc, uint managedReadBufferSize);
//        private delegate bool get_isStreamedSceneAssetBundleDelegate(IntPtr _param1);
//        private delegate bool ContainsDelegate(IntPtr _param1, IntPtr name);
//        private delegate IntPtr LoadAsset_InternalDelegate(IntPtr _param1, IntPtr name, IntPtr type);
//        private delegate IntPtr UnloadDelegate(IntPtr _param1, bool unloadAllLoadedObjects);
//        private delegate IntPtr GetAllAssetNamesDelegate(IntPtr _param1);
//        private delegate IntPtr GetAllScenePathsDelegate(IntPtr _param1);
//        private delegate IntPtr LoadAssetWithSubAssets_InternalDelegate(IntPtr _param1, IntPtr name, IntPtr type);

//        private static readonly returnMainAssetDelegate dg_returnMainAsset = IL2CPP.ResolveICall<returnMainAssetDelegate>("UnityEngine.AssetBundle::returnMainAsset");
//        private static readonly UnloadAllAssetBundlesDelegate dg_UnloadAllAssetBundles = IL2CPP.ResolveICall<UnloadAllAssetBundlesDelegate>("UnityEngine.AssetBundle::UnloadAllAssetBundles");
//        private static readonly GetAllLoadedAssetBundles_NativeDelegate dg_GetAllLoadedAssetBundles_Native = IL2CPP.ResolveICall<GetAllLoadedAssetBundles_NativeDelegate>("UnityEngine.AssetBundle::GetAllLoadedAssetBundles_Native");
//        private static readonly LoadFromFile_InternalDelegate dg_LoadFromFile_Internal = IL2CPP.ResolveICall<LoadFromFile_InternalDelegate>("UnityEngine.AssetBundle::LoadFromFile_Internal");
//        private static readonly LoadFromMemory_InternalDelegate dg_LoadFromMemory_Internal = IL2CPP.ResolveICall<LoadFromMemory_InternalDelegate>("UnityEngine.AssetBundle::LoadFromMemory_Internal");
//        private static readonly LoadFromStreamInternalDelegate dg_LoadFromStreamInternal = IL2CPP.ResolveICall<LoadFromStreamInternalDelegate>("UnityEngine.AssetBundle::LoadFromStreamInternal");
//        private static readonly get_isStreamedSceneAssetBundleDelegate dg_get_isStreamedSceneAssetBundle = IL2CPP.ResolveICall<get_isStreamedSceneAssetBundleDelegate>("UnityEngine.AssetBundle::get_isStreamedSceneAssetBundle");
//        private static readonly ContainsDelegate dg_Contains = IL2CPP.ResolveICall<ContainsDelegate>("UnityEngine.AssetBundle::Contains");
//        private static readonly LoadAsset_InternalDelegate dg_LoadAsset_Internal = IL2CPP.ResolveICall<LoadAsset_InternalDelegate>("UnityEngine.AssetBundle::LoadAsset_Internal");
//        private static readonly UnloadDelegate dg_Unload = IL2CPP.ResolveICall<UnloadDelegate>("UnityEngine.AssetBundle::Unload");
//        private static readonly GetAllAssetNamesDelegate dg_GetAllAssetNames = IL2CPP.ResolveICall<GetAllAssetNamesDelegate>("UnityEngine.AssetBundle::GetAllAssetNames");
//        private static readonly GetAllScenePathsDelegate dg_GetAllScenePaths = IL2CPP.ResolveICall<GetAllScenePathsDelegate>("UnityEngine.AssetBundle::GetAllScenePaths");
//        private static readonly LoadAssetWithSubAssets_InternalDelegate dg_LoadAssetWithSubAssets_Internal = IL2CPP.ResolveICall<LoadAssetWithSubAssets_InternalDelegate>("UnityEngine.AssetBundle::LoadAssetWithSubAssets_Internal");

//        private IntPtr bundlePtr;

//        public AssetBundleProxy(IntPtr bundlePtr)
//        {
//            this.bundlePtr = bundlePtr;
//        }

//        public static AssetBundleProxy LoadFromFile(string path, uint crc = 0u, ulong offset = 0ul)
//        {
//            var bundlePtr = dg_LoadFromFile_Internal.Invoke(IL2CPP.ManagedStringToIl2Cpp(path), crc, offset);
//            if (bundlePtr == IntPtr.Zero) return null;

//            return new AssetBundleProxy(bundlePtr);
//        }

//        public static AssetBundleProxy LoadFromMemory(byte[] data, uint crc = 0u)
//        {
//            var bundlePtr = dg_LoadFromMemory_Internal.Invoke(IL2CPP.Il2CppObjectBaseToPtr(new Il2CppStructArray<byte>(data)), crc);
//            if (bundlePtr == IntPtr.Zero) return null;

//            return new AssetBundleProxy(bundlePtr);
//        }

//        public string[] GetAllAssetNames()
//        {
//            var namesPtr = dg_GetAllAssetNames.Invoke(bundlePtr);
//            return namesPtr == IntPtr.Zero ? null : new string[](namesPtr);
//        }

//        public UnityEngine.Object LoadAsset(string path, Type type) => LoadAsset(path, Il2CppType.From(type));
//        public UnityEngine.Object LoadAsset(string path) => LoadAsset(path, Il2CppType.Of<UnityEngine.Object>());
//        public UnityEngine.Object LoadAsset(string path, Il2CppSystem.Type il2cppType)
//        {
//            var assetPtr = dg_LoadAsset_Internal.Invoke(bundlePtr, IL2CPP.ManagedStringToIl2Cpp(path), IL2CPP.Il2CppObjectBaseToPtr(il2cppType));
//            return assetPtr == IntPtr.Zero ? null : new UnityEngine.Object(assetPtr);
//        }

//        public T LoadAsset<T>(string path) where T : UnityEngine.Object
//        {
//            var asset = LoadAsset(path, Il2CppType.Of<T>());
//            return asset == null ? null : asset.Cast<T>();
//        }

//        public UnityEngine.Object[] LoadAssetWithSubAssets(string name) => LoadAssetWithSubAssets(name, Il2CppType.Of<UnityEngine.Object>());
//        public UnityEngine.Object[] LoadAssetWithSubAssets(string name, Type type) => LoadAssetWithSubAssets(name, Il2CppType.From(type));
//        public UnityEngine.Object[] LoadAssetWithSubAssets(string name, Il2CppSystem.Type il2cppType)
//        {
//            if (name == "") throw new ArgumentException("The input asset name cannot be empty.");
//            if (name == null) throw new NullReferenceException("The input asset name cannot be null.");
//            if (il2cppType == null) throw new NullReferenceException("The input type cannot be null.");

//            var assetsPtr = dg_LoadAssetWithSubAssets_Internal.Invoke(bundlePtr, IL2CPP.ManagedStringToIl2Cpp(name), IL2CPP.Il2CppObjectBaseToPtr(il2cppType));
//            return assetsPtr == IntPtr.Zero ? null : new UnityEngine.Object[](assetsPtr);
//        }

//        public System.Collections.Generic.IEnumerable<T> LoadAllAssets<T>() where T : UnityEngine.Object => LoadAllAssets(Il2CppType.Of<T>()).OfType<T>();
//        public UnityEngine.Object[] LoadAllAssets() => LoadAllAssets(Il2CppType.Of<UnityEngine.Object>());
//        public UnityEngine.Object[] LoadAllAssets(Type type) => LoadAllAssets(Il2CppType.From(type));
//        public UnityEngine.Object[] LoadAllAssets(Il2CppSystem.Type type)
//        {
//            if (type == null) throw new NullReferenceException("The input type cannot be null.");

//            var assetsPtr = dg_LoadAssetWithSubAssets_Internal.Invoke(bundlePtr, IL2CPP.ManagedStringToIl2Cpp(""), IL2CPP.Il2CppObjectBaseToPtr(type));
//            return assetsPtr == IntPtr.Zero ? null : new UnityEngine.Object[](assetsPtr);
//        }
//    }
//}
