// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Collections.Generic;
using UnityEditor.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.TextCore.Text
{
    static class EditorEventCallbacks
    {
        [InitializeOnLoadMethod]
        internal static void InitializeFontAssetResourceChangeCallBacks()
        {
            FontAsset.RegisterResourceForUpdate += TextEditorResourceManager.RegisterResourceForUpdate;
            FontAsset.RegisterResourceForReimport += TextEditorResourceManager.RegisterResourceForReimport;
            FontAsset.OnFontAssetTextureChanged += TextEditorResourceManager.AddTextureToAsset;
            FontAsset.SetAtlasTextureIsReadable += FontEngineEditorUtilities.SetAtlasTextureIsReadable;
            FontAsset.GetSourceFontRef += TextEditorResourceManager.GetSourceFontRef;
            FontAsset.SetSourceFontGUID += TextEditorResourceManager.SetSourceFontGUID;
            FontAsset.EditorApplicationIsUpdating += () => EditorApplication.isUpdating;

            // Callback to handle clearing dynamic font asset data when closing the Editor
            EditorApplication.quitting += () =>
            {
                // Find all font assets in the project
                string searchPattern = "t:FontAsset";
                string[] fontAssetGUIDs = AssetDatabase.FindAssets(searchPattern);

                for (int i = 0; i < fontAssetGUIDs.Length; i++)
                {
                    string fontAssetPath = AssetDatabase.GUIDToAssetPath(fontAssetGUIDs[i]);
                    FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(fontAssetPath);

                    if (fontAsset != null && (fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic || fontAsset.atlasPopulationMode == AtlasPopulationMode.DynamicOS) && fontAsset.clearDynamicDataOnBuild && fontAsset.atlasTexture.width > 1)
                    {
                        Debug.Log("Clearing [" + fontAsset.name + "] dynamic font asset data.");
                        fontAsset.ClearCharacterAndGlyphTablesInternal();
                    }
                }
            };
        }
    }

    internal class TextEditorResourceManager
    {
        private static TextEditorResourceManager s_Instance;

        private readonly List<Object> m_ObjectUpdateQueue = new List<Object>();
        private HashSet<int> m_ObjectUpdateQueueLookup = new HashSet<int>();

        private readonly List<Object> m_ObjectReImportQueue = new List<Object>();
        private HashSet<int> m_ObjectReImportQueueLookup = new HashSet<int>();

        private readonly List<FontAsset> m_FontAssetDefinitionRefreshQueue = new List<FontAsset>();
        private HashSet<int> m_FontAssetDefinitionRefreshQueueLookup = new HashSet<int>();

        /// <summary>
        /// Get a singleton instance of the manager.
        /// </summary>
        public static TextEditorResourceManager instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new TextEditorResourceManager();

                return s_Instance;
            }
        }

        /// <summary>
        /// Register to receive rendering callbacks.
        /// </summary>
        private TextEditorResourceManager()
        {
            // Register to the appropriate callback for the given render pipeline.
            if (RenderPipelineManager.currentPipeline == null)
                Camera.onPostRender += OnCameraPostRender;
            else
                RenderPipelineManager.endContextRendering += OnEndOfFrame;

            Canvas.willRenderCanvases += OnPreRenderCanvases;
        }

        void OnCameraPostRender(Camera cam)
        {
            // Exclude the PreRenderCamera
            if (cam.cameraType != CameraType.SceneView)
                return;

            DoPostRenderUpdates();
        }

        void OnPreRenderCanvases()
        {
            DoPreRenderUpdates();
        }

        void OnEndOfFrame(ScriptableRenderContext renderContext, List<Camera> cameras)
        {
            DoPostRenderUpdates();
        }

        /// <summary>
        /// Register resource for re-import.
        /// </summary>
        /// <param name="obj"></param>
        internal static void RegisterResourceForReimport(Object obj)
        {
            // Return if referenced object is not a persistent asset
            if (!EditorUtility.IsPersistent(obj))
                return;

            instance.InternalRegisterResourceForReimport(obj);
        }

        private void InternalRegisterResourceForReimport(Object obj)
        {
            int id = obj.GetInstanceID();

            if (m_ObjectReImportQueueLookup.Contains(id))
                return;

            m_ObjectReImportQueueLookup.Add(id);
            m_ObjectReImportQueue.Add(obj);
        }

        /// <summary>
        /// Register resource to be updated.
        /// </summary>
        /// <param name="obj"></param>
        internal static void RegisterResourceForUpdate(Object obj)
        {
            // Return if referenced object is not a persistent asset
            if (!EditorUtility.IsPersistent(obj))
                return;

            instance.InternalRegisterResourceForUpdate(obj);
        }

        private void InternalRegisterResourceForUpdate(Object obj)
        {
            int id = obj.GetInstanceID();

            if (m_ObjectUpdateQueueLookup.Contains(id))
                return;

            m_ObjectUpdateQueueLookup.Add(id);
            m_ObjectUpdateQueue.Add(obj);
        }

        /// <summary>
        /// Register font asset that require re-reading their definitions.
        /// </summary>
        /// <param name="fontAsset"></param>
        internal static void RegisterFontAssetForDefinitionRefresh(FontAsset fontAsset)
        {
            instance.InternalRegisterFontAssetForDefinitionRefresh(fontAsset);
        }

        private void InternalRegisterFontAssetForDefinitionRefresh(FontAsset fontAsset)
        {
            int id = fontAsset.GetInstanceID();

            if (m_FontAssetDefinitionRefreshQueueLookup.Contains(id))
                return;

            m_FontAssetDefinitionRefreshQueueLookup.Add(id);
            m_FontAssetDefinitionRefreshQueue.Add(fontAsset);
        }

        /// <summary>
        /// Add texture as sub asset to the referenced object.
        /// </summary>
        /// <param name="tex">The texture to be added as sub object.</param>
        /// <param name="obj">The object to which this texture sub object will be added.</param>
        internal static void AddTextureToAsset(Texture tex, Object obj)
        {
            // Return if referenced object is not a persistent asset
            if (!EditorUtility.IsPersistent(obj) || (obj.hideFlags & HideFlags.DontSave) != 0)
                return;

            if (tex != null)
                AssetDatabase.AddObjectToAsset(tex, obj);

            RegisterResourceForReimport(obj);
        }

        internal static Font GetSourceFontRef(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        internal static string SetSourceFontGUID(Font font)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font));
        }

        void DoPostRenderUpdates()
        {
            // Handle objects that need updating
            int objUpdateCount = m_ObjectUpdateQueue.Count;

            for (int i = 0; i < objUpdateCount; i++)
            {
                TextCorePropertyDrawerUtilities.s_RefreshGlyphProxyLookup = true;
                UnityEditor.TextCore.Text.TextCorePropertyDrawerUtilities.s_RefreshGlyphProxyLookup = true;

                Object obj = m_ObjectUpdateQueue[i];
                if (obj != null)
                {
                    //EditorUtility.SetDirty(obj);
                }
            }

            if (objUpdateCount > 0)
            {
                //Debug.Log("Saving assets");
                //AssetDatabase.SaveAssets();

                m_ObjectUpdateQueue.Clear();
                m_ObjectUpdateQueueLookup.Clear();
            }

            // Handle objects that need re-importing
            int objReImportCount = m_ObjectReImportQueue.Count;

            for (int i = 0; i < objReImportCount; i++)
            {
                Object obj = m_ObjectReImportQueue[i];
                if (obj != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(obj);

                    // Exclude Assets not located in the project
                    if (assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                        AssetDatabase.ImportAsset(assetPath);
                }
            }

            if (objReImportCount > 0)
            {
                m_ObjectReImportQueue.Clear();
                m_ObjectReImportQueueLookup.Clear();
            }
        }

        void DoPreRenderUpdates()
        {
            // Handle Font Asset Definition Refresh
            for (int i = 0; i < m_FontAssetDefinitionRefreshQueue.Count; i++)
            {
                FontAsset fontAsset = m_FontAssetDefinitionRefreshQueue[i];

                if (fontAsset != null)
                {
                    fontAsset.ReadFontAssetDefinition();
                    TextEventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
                }
            }

            if (m_FontAssetDefinitionRefreshQueue.Count > 0)
            {
                m_FontAssetDefinitionRefreshQueue.Clear();
                m_FontAssetDefinitionRefreshQueueLookup.Clear();
            }
        }
    }
}
