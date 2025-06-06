// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Bindings;

namespace UnityEngine.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AttachmentDescriptor : IEquatable<AttachmentDescriptor>
    {
        RenderBufferLoadAction m_LoadAction;
        RenderBufferStoreAction m_StoreAction;
        GraphicsFormat m_Format;
        RenderTargetIdentifier m_LoadStoreTarget;
        RenderTargetIdentifier m_ResolveTarget;
        Color m_ClearColor;
        float m_ClearDepth;
        uint m_ClearStencil;

        // The Load action to use when this attachment is accessed for the first time in this renderpass
        public RenderBufferLoadAction loadAction
        {
            get { return m_LoadAction; }
            set { m_LoadAction = value; }
        }

        // Store action to use when the the last subpass that accesses this attachment is done.
        // Note that this isn't set by the constructor, but rather by calling SetStoreTarget/SetResolveTarget
        public RenderBufferStoreAction storeAction
        {
            get { return m_StoreAction; }
            set { m_StoreAction = value; }
        }

        // The format of this attachment
        public GraphicsFormat graphicsFormat
        {
            get { return m_Format; }
            set { m_Format = value; }
        }

        // The format of this attachment, legacy.
        public RenderTextureFormat format
        {
            get
            {
                if (GraphicsFormatUtility.IsDepthStencilFormat(m_Format))
                {
                    return RenderTextureFormat.Depth;
                    // Note that there is no way for us to identify the descriptor as "RenderTextureFormat.ShadowMap".
                    // This is because the ShadowSamplingMode is not relevant to AttachmentDescriptors. (attachments aren't sampled)
                }
                return GraphicsFormatUtility.GetRenderTextureFormat(m_Format);
            }
            set { m_Format = GetAdjustedFormat(value, RenderTextureReadWrite.Default); }
        }

        // The render texture where the load/store operations take place
        public RenderTargetIdentifier loadStoreTarget
        {
            get { return m_LoadStoreTarget; }
            set { m_LoadStoreTarget = value; }
        }

        // The render texture to resolve the attachment into at the end of the renderpass
        public RenderTargetIdentifier resolveTarget
        {
            get { return m_ResolveTarget; }
            set { m_ResolveTarget = value; }
        }

        // If loadAction is set to clear and this is a color surface, clear it to this color
        public Color clearColor
        {
            get { return m_ClearColor; }
            set { m_ClearColor = value; }
        }

        // If loadAction is set to clear and this is a depth surface, clear to this value
        public float clearDepth
        {
            get { return m_ClearDepth; }
            set { m_ClearDepth = value; }
        }

        // If loadAction is set to clear and this is a depth+stencil surface, clear to this value
        public uint clearStencil
        {
            get { return m_ClearStencil; }
            set { m_ClearStencil = value; }
        }

        // Bind a backing surface for this attachment. If none is set, the attachment is transient / memoryless (where supported)
        // or a temporary surface that's released at the end of the renderpass.
        // If loadExistingContents is true, the current contents of the surface is loaded as the initial pixel values for the attachment,
        // otherwise the initial values are undefined (with the expectation that the renderpass will render to every pixel on the screen)
        // If storeResults is true, the attachment contents at the end of the renderpass are stored to the surface,
        // otherwise the contents of the surface are undefined after the end of the renderpass.
        public void ConfigureTarget(RenderTargetIdentifier target, bool loadExistingContents, bool storeResults)
        {
            m_LoadStoreTarget = target;
            if (loadExistingContents && m_LoadAction != RenderBufferLoadAction.Clear)
                m_LoadAction = RenderBufferLoadAction.Load;
            if (storeResults)
            {
                if (m_StoreAction == RenderBufferStoreAction.StoreAndResolve || m_StoreAction == RenderBufferStoreAction.Resolve)
                    m_StoreAction = RenderBufferStoreAction.StoreAndResolve;
                else
                    m_StoreAction = RenderBufferStoreAction.Store;
            }
        }

        // If the renderpass has MSAA enabled, AA-resolve this attachment into the given render target.
        public void ConfigureResolveTarget(RenderTargetIdentifier target)
        {
            m_ResolveTarget = target;
            if (m_StoreAction == RenderBufferStoreAction.StoreAndResolve || m_StoreAction == RenderBufferStoreAction.Store)
                m_StoreAction = RenderBufferStoreAction.StoreAndResolve;
            else
                m_StoreAction = RenderBufferStoreAction.Resolve;
        }

        // At the beginning of the renderpass, clear this attachment with the given clear color (or depth/stencil)
        public void ConfigureClear(Color clearColor, float clearDepth = 1.0f, uint clearStencil = 0)
        {
            m_ClearColor = clearColor;
            m_ClearDepth = clearDepth;
            m_ClearStencil = clearStencil;
            m_LoadAction = RenderBufferLoadAction.Clear;
        }

        public AttachmentDescriptor(GraphicsFormat format)
            : this()
        {
            m_LoadAction = RenderBufferLoadAction.DontCare;
            m_StoreAction = RenderBufferStoreAction.DontCare;
            m_Format = format;
            m_LoadStoreTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.None);
            m_ResolveTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.None);
            m_ClearColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            m_ClearDepth = 1.0f;
        }

        public AttachmentDescriptor(RenderTextureFormat format)
            : this(GetAdjustedFormat(format, RenderTextureReadWrite.Default))
        {
        }

        public AttachmentDescriptor(RenderTextureFormat format, RenderTargetIdentifier target, bool loadExistingContents = false, bool storeResults = false, bool resolve = false)
            : this(GetAdjustedFormat(format, RenderTextureReadWrite.Default))
        {}

        private static GraphicsFormat GetAdjustedFormat(RenderTextureFormat format, RenderTextureReadWrite readWrite)
        {
            switch (format)
            {
                case RenderTextureFormat.Depth:
                case RenderTextureFormat.Shadowmap:
                    // Outside of the AttachmentDescriptor case, RTF.Depth / RTF.Shadowmap don't equate to the DefaultFormats. We need this to keep the behavior intact though.
                    return SystemInfo.GetGraphicsFormat(format == RenderTextureFormat.Depth ? DefaultFormat.DepthStencil : DefaultFormat.Shadow);
                default:
                    return GraphicsFormatUtility.GetGraphicsFormat(format, readWrite);
            }
        }

        public bool Equals(AttachmentDescriptor other)
        {
            return m_LoadAction == other.m_LoadAction && m_StoreAction == other.m_StoreAction && m_Format == other.m_Format && m_LoadStoreTarget.Equals(other.m_LoadStoreTarget) && m_ResolveTarget.Equals(other.m_ResolveTarget) && m_ClearColor.Equals(other.m_ClearColor) && m_ClearDepth.Equals(other.m_ClearDepth) && m_ClearStencil == other.m_ClearStencil;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is AttachmentDescriptor && Equals((AttachmentDescriptor)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)m_LoadAction;
                hashCode = (hashCode * 397) ^ (int)m_StoreAction;
                hashCode = (hashCode * 397) ^ (int)m_Format;
                hashCode = (hashCode * 397) ^ m_LoadStoreTarget.GetHashCode();
                hashCode = (hashCode * 397) ^ m_ResolveTarget.GetHashCode();
                hashCode = (hashCode * 397) ^ m_ClearColor.GetHashCode();
                hashCode = (hashCode * 397) ^ m_ClearDepth.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)m_ClearStencil;
                return hashCode;
            }
        }

        public static bool operator==(AttachmentDescriptor left, AttachmentDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator!=(AttachmentDescriptor left, AttachmentDescriptor right)
        {
            return !left.Equals(right);
        }
    }

    [Flags]
    public enum SubPassFlags
    {
        None = 0,
        ReadOnlyDepth = 1 << 1,
        ReadOnlyStencil = 1 << 2,
        ReadOnlyDepthStencil = ReadOnlyDepth | ReadOnlyStencil,
        UseShadingRateImage = 1 << 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AttachmentIndexArray
    {
        public static AttachmentIndexArray Emtpy = new AttachmentIndexArray(0);

        // This is a fixed size struct that emulates itself as an array
        // similar to how Unity.Math emulates size arrays
        // KEEP IN SYNC with existing c++ code, 8 for now
        public const int MaxAttachments = 8;
        private int a0, a1, a2, a3, a4, a5, a6, a7;
        private int activeAttachments;

        public AttachmentIndexArray(int numAttachments)
        {
            if (numAttachments < 0 || numAttachments > MaxAttachments)
            {
                throw new ArgumentException($"AttachmentIndexArray - numAttachments must be in range of [0, {MaxAttachments}[");
            }
            a0 = a1 = a2 = a3 = a4 = a5 = a6 = a7 = -1;
            activeAttachments = numAttachments;
        }
        public AttachmentIndexArray(int[] attachments) : this(attachments.Length)
        {
            for (int i = 0; i < activeAttachments; ++i)
            {
                this[i] = attachments[i];
            }
        }
        public AttachmentIndexArray(NativeArray<int> attachments) : this(attachments.Length)
        {
            for (int i = 0; i < activeAttachments; ++i)
            {
                this[i] = attachments[i];
            }
        }

        public int this[int index]
        {
            get
            {
                if ((uint)index >= MaxAttachments)
                    throw new IndexOutOfRangeException($"AttachmentIndexArray - index must be in range of [0, {MaxAttachments}[");
                if ((uint)index >= activeAttachments)
                    throw new IndexOutOfRangeException($"AttachmentIndexArray - index must be in range of [0, {activeAttachments}[");
                unsafe
                {
                    fixed (AttachmentIndexArray* self = &this)
                    {
                        int* array = (int*)self;
                        return array[index];
                    }
                }
            }
            set
            {
                if ((uint)index >= MaxAttachments)
                    throw new IndexOutOfRangeException($"AttachmentIndexArray - index must be in range of [0, {MaxAttachments}[");
                if ((uint)index >= activeAttachments)
                    throw new IndexOutOfRangeException($"AttachmentIndexArray - index must be in range of [0, {activeAttachments}[");
                unsafe
                {
                    fixed (AttachmentIndexArray* self = &this)
                    {
                        int* array = (int*)self;
                        array[index] = value;
                    }
                }
            }
        }

        public int Length
        {
            get
            {
                return activeAttachments;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SubPassDescriptor
    {
        public AttachmentIndexArray inputs;
        public AttachmentIndexArray colorOutputs;
        public SubPassFlags flags;
    }
}
