using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Serialization;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Rendering;

namespace UnityEngine.XR.ARFoundation
{
    /// <summary>
    /// The manager for the occlusion subsystem.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ARUpdateOrder.k_OcclusionManager)]
    [HelpURL(HelpUrls.ApiWithNamespace + nameof(AROcclusionManager) + ".html")]
    public sealed class AROcclusionManager :
#if UNITY_2020_2_OR_NEWER
        SubsystemLifecycleManager<XROcclusionSubsystem, XROcclusionSubsystemDescriptor, XROcclusionSubsystem.Provider>
#else
        SubsystemLifecycleManager<XROcclusionSubsystem, XROcclusionSubsystemDescriptor>
#endif
    {
        /// <summary>
        /// The list of occlusion texture infos.
        /// </summary>
        /// <value>
        /// The list of occlusion texture infos.
        /// </value>
        readonly List<ARTextureInfo> m_TextureInfos = new List<ARTextureInfo>();

        /// <summary>
        /// The list of occlusion textures.
        /// </summary>
        /// <value>
        /// The list of occlusion textures.
        /// </value>
        readonly List<Texture2D> m_Textures = new List<Texture2D>();

        /// <summary>
        /// The list of occlusion texture property IDs.
        /// </summary>
        /// <value>
        /// The list of occlusion texture property IDs.
        /// </value>
        readonly List<int> m_TexturePropertyIds = new List<int>();

        /// <summary>
        /// The human stencil texture info.
        /// </summary>
        /// <value>
        /// The human stencil texture info.
        /// </value>
        ARTextureInfo m_HumanStencilTextureInfo;

        /// <summary>
        /// The human depth texture info.
        /// </summary>
        /// <value>
        /// The human depth texture info.
        /// </value>
        ARTextureInfo m_HumanDepthTextureInfo;

        /// <summary>
        /// An event which fires each time an occlusion camera frame is received.
        /// </summary>
        public event Action<AROcclusionFrameEventArgs> frameReceived;

        /// <summary>
        /// The mode for generating the human segmentation stencil texture.
        /// This method is obsolete.
        /// Use <see cref="requestedHumanStencilMode"/>
        /// or  <see cref="currentHumanStencilMode"/> instead.
        /// </summary>
        [Obsolete("Use requestedSegmentationStencilMode or currentSegmentationStencilMode instead. (2020-01-14)")]
        public HumanSegmentationStencilMode humanSegmentationStencilMode
        {
            get => m_HumanSegmentationStencilMode;
            set => requestedHumanStencilMode = value;
        }

        /// <summary>
        /// The requested mode for generating the human segmentation stencil texture.
        /// </summary>
        public HumanSegmentationStencilMode requestedHumanStencilMode
        {
            get => subsystem?.requestedHumanStencilMode ?? m_HumanSegmentationStencilMode;
            set
            {
                m_HumanSegmentationStencilMode = value;
                if (enabled && descriptor?.supportsHumanSegmentationStencilImage == true)
                {
                    subsystem.requestedHumanStencilMode = value;
                }
            }
        }

        /// <summary>
        /// Get the current mode in use for generating the human segmentation stencil mode.
        /// </summary>
        public HumanSegmentationStencilMode currentHumanStencilMode => subsystem?.currentHumanStencilMode ?? HumanSegmentationStencilMode.Disabled;

        [SerializeField]
        [Tooltip("The mode for generating human segmentation stencil texture.\n\n"
                 + "Disabled -- No human stencil texture produced.\n"
                 + "Fastest -- Minimal rendering quality. Minimal frame computation.\n"
                 + "Medium -- Medium rendering quality. Medium frame computation.\n"
                 + "Best -- Best rendering quality. Increased frame computation.")]
        HumanSegmentationStencilMode m_HumanSegmentationStencilMode = HumanSegmentationStencilMode.Fastest;

        /// <summary>
        /// The mode for generating the human segmentation depth texture.
        /// This method is obsolete.
        /// Use <see cref="requestedHumanDepthMode"/>
        /// or  <see cref="currentHumanDepthMode"/> instead.
        /// </summary>
        [Obsolete("Use requestedSegmentationDepthMode or currentSegmentationDepthMode instead. (2020-01-15)")]
        public HumanSegmentationDepthMode humanSegmentationDepthMode
        {
            get => m_HumanSegmentationDepthMode;
            set => requestedHumanDepthMode = value;
        }

        /// <summary>
        /// Get or set the requested human segmentation depth mode.
        /// </summary>
        public HumanSegmentationDepthMode requestedHumanDepthMode
        {
            get => subsystem?.requestedHumanDepthMode ?? m_HumanSegmentationDepthMode;
            set
            {
                m_HumanSegmentationDepthMode = value;
                if (enabled && descriptor?.supportsHumanSegmentationDepthImage == true)
                {
                    subsystem.requestedHumanDepthMode = value;
                }
            }
        }

        /// <summary>
        /// Get the current human segmentation depth mode in use by the subsystem.
        /// </summary>
        public HumanSegmentationDepthMode currentHumanDepthMode => subsystem?.currentHumanDepthMode ?? HumanSegmentationDepthMode.Disabled;

        [SerializeField]
        [Tooltip("The mode for generating human segmentation depth texture.\n\n"
                 + "Disabled -- No human depth texture produced.\n"
                 + "Fastest -- Minimal rendering quality. Minimal frame computation.\n"
                 + "Best -- Best rendering quality. Increased frame computation.")]
        HumanSegmentationDepthMode m_HumanSegmentationDepthMode = HumanSegmentationDepthMode.Fastest;

        /// <summary>
        /// The human segmentation stencil texture.
        /// </summary>
        /// <value>
        /// The human segmentation stencil texture, if any. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D humanStencilTexture
        {
            get
            {
                if ((subsystem != null) && subsystem.TryGetHumanStencil(out XRTextureDescriptor humanStencilDescriptor))
                {
                    m_HumanStencilTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(m_HumanStencilTextureInfo,
                                                                                    humanStencilDescriptor);
                    Debug.Assert(m_HumanStencilTextureInfo.descriptor.dimension == TextureDimension.Tex2D, $"Human Stencil Texture needs to be a Texture 2D, but instead is {m_HumanStencilTextureInfo.descriptor.dimension.ToString()}.");
                    return (Texture2D) m_HumanStencilTextureInfo.texture;
                }
                return null;
            }
        }

        /// <summary>
        /// Attempt to get the latest human stencil CPU image. This provides directly access to the raw pixel data.
        /// </summary>
        /// <remarks>
        /// The `XRCpuImage` must be disposed to avoid resource leaks.
        /// </remarks>
        /// <param name="cpuImage">If this method returns `true`, an acquired `XRCpuImage`.</param>
        /// <returns>Returns `true` if the CPU image was acquired. Returns `false` otherwise.</returns>
        public bool TryAcquireHumanStencilCpuImage(out XRCpuImage cpuImage)
        {
            if (subsystem == null)
            {
                cpuImage = default;
                return false;
            }

            return subsystem.TryAcquireHumanStencilCpuImage(out cpuImage);
        }

        /// <summary>
        /// The human segmentation depth texture.
        /// </summary>
        /// <value>
        /// The human segmentation depth texture, if any. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D humanDepthTexture
        {
            get
            {
                if ((subsystem != null) && subsystem.TryGetHumanDepth(out XRTextureDescriptor humanDepthDescriptor))
                {
                    m_HumanDepthTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(m_HumanDepthTextureInfo,
                                                                                  humanDepthDescriptor);
                    Debug.Assert(m_HumanDepthTextureInfo.descriptor.dimension == TextureDimension.Tex2D, $"Human Depth Texture needs to be a Texture 2D, but instead is {m_HumanDepthTextureInfo.descriptor.dimension.ToString()}.");
                    return (Texture2D) m_HumanDepthTextureInfo.texture;
                }
                return null;
            }
        }

        /// <summary>
        /// Attempt to get the latest human depth CPU image. This provides directly access to the raw pixel data.
        /// </summary>
        /// <remarks>
        /// The `XRCpuImage` must be disposed to avoid resource leaks.
        /// </remarks>
        /// <param name="cpuImage">If this method returns `true`, an acquired `XRCpuImage`.</param>
        /// <returns>Returns `true` if the CPU image was acquired. Returns `false` otherwise.</returns>
        public bool TryAcquireHumanDepthCpuImage(out XRCpuImage cpuImage)
        {
            if (subsystem == null)
            {
                cpuImage = default;
                return false;
            }

            return subsystem.TryAcquireHumanDepthCpuImage(out cpuImage);
        }

        /// <summary>
        /// Callback before the subsystem is started (but after it is created).
        /// </summary>
        protected override void OnBeforeStart()
        {
            if (descriptor.supportsHumanSegmentationStencilImage)
            {
                subsystem.requestedHumanStencilMode = m_HumanSegmentationStencilMode;
            }
            if (descriptor.supportsHumanSegmentationDepthImage)
            {
                subsystem.requestedHumanDepthMode = m_HumanSegmentationDepthMode;
            }

            m_HumanStencilTextureInfo.Reset();
            m_HumanDepthTextureInfo.Reset();
        }

        /// <summary>
        /// Callback when the manager is being disabled.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            m_HumanStencilTextureInfo.Reset();
            m_HumanDepthTextureInfo.Reset();
        }

        /// <summary>
        /// Callback as the manager is being updated.
        /// </summary>
        public void Update()
        {
            if (subsystem != null)
            {
                UpdateTexturesInfos();
                InvokeFrameReceived();
            }
        }

        /// <summary>
        /// Pull the texture descriptors from the occlusion subsystem, and update the texture information maintained by
        /// this component.
        /// </summary>
        void UpdateTexturesInfos()
        {
            var textureDescriptors = subsystem.GetTextureDescriptors(Allocator.Temp);
            try
            {
                int numUpdated = Math.Min(m_TextureInfos.Count, textureDescriptors.Length);

                // Update the existing textures that are in common between the two arrays.
                for (int i = 0; i < numUpdated; ++i)
                {
                    m_TextureInfos[i] = ARTextureInfo.GetUpdatedTextureInfo(m_TextureInfos[i], textureDescriptors[i]);
                }

                // If there are fewer textures in the current frame than we had previously, destroy any remaining unneeded
                // textures.
                if (numUpdated < m_TextureInfos.Count)
                {
                    for (int i = numUpdated; i < m_TextureInfos.Count; ++i)
                    {
                        m_TextureInfos[i].Reset();
                    }
                    m_TextureInfos.RemoveRange(numUpdated, (m_TextureInfos.Count - numUpdated));
                }
                // Else, if there are more textures in the current frame than we have previously, add new textures for any
                // additional descriptors.
                else if (textureDescriptors.Length > m_TextureInfos.Count)
                {
                    for (int i = numUpdated; i < textureDescriptors.Length; ++i)
                    {
                        m_TextureInfos.Add(new ARTextureInfo(textureDescriptors[i]));
                    }
                }
            }
            finally
            {
                if (textureDescriptors.IsCreated)
                {
                    textureDescriptors.Dispose();
                }
            }
        }

        /// <summary>
        /// Invoke the occlusion frame received event with the updated textures and texture property IDs.
        /// </summary>
        void InvokeFrameReceived()
        {
            if (frameReceived != null)
            {
                int numTextureInfos = m_TextureInfos.Count;

                m_Textures.Clear();
                m_TexturePropertyIds.Clear();

                m_Textures.Capacity = numTextureInfos;
                m_TexturePropertyIds.Capacity = numTextureInfos;

                for (int i = 0; i < numTextureInfos; ++i)
                {
                    Debug.Assert(m_TextureInfos[i].descriptor.dimension == TextureDimension.Tex2D, $"Texture needs to be a Texture 2D, but instead is {m_TextureInfos[i].descriptor.dimension.ToString()}.");
                    m_Textures.Add((Texture2D)m_TextureInfos[i].texture);
                    m_TexturePropertyIds.Add(m_TextureInfos[i].descriptor.propertyNameId);
                }

                subsystem.GetMaterialKeywords(out List<string> enabledMaterialKeywords, out List<string>disabledMaterialKeywords);

                AROcclusionFrameEventArgs args = new AROcclusionFrameEventArgs();
                args.textures = m_Textures;
                args.propertyNameIds = m_TexturePropertyIds;
                args.enabledMaterialKeywords = enabledMaterialKeywords;
                args.disabledMaterialKeywords = disabledMaterialKeywords;

                frameReceived(args);
            }
        }
    }
}
