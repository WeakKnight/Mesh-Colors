using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class ReyesPipeline : RenderPipeline
{
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var camera in cameras) 
        {
            if (camera != null) 
            {
                RenderCamera(context, camera);
            }
        }
    }

    void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        context.SetupCameraProperties(camera);

        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            return;
        }

        var commandBuffer = new CommandBuffer { name = camera.name };

        CameraClearFlags clearFlags = camera.clearFlags;
        commandBuffer.ClearRenderTarget(
            (clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor
        );

        CullingResults cullingResults = context.Cull(ref p);

        {
            commandBuffer.BeginSample("Pre Z");

            RendererListDesc desc = new RendererListDesc(new ShaderTagId("PreZ"), cullingResults, camera)
            {
                rendererConfiguration = PerObjectData.None,
                renderQueueRange = RenderQueueRange.opaque,
                sortingCriteria = SortingCriteria.CommonOpaque,
            };

            RendererList rendererList = context.CreateRendererList(desc);

            commandBuffer.DrawRendererList(rendererList);

            commandBuffer.EndSample("Pre Z");
        }

        {
            commandBuffer.BeginSample("Forward Shading");

            RendererListDesc desc = new RendererListDesc(new ShaderTagId("ForwardShading"), cullingResults, camera)
            {
                rendererConfiguration = PerObjectData.None,
                renderQueueRange = RenderQueueRange.opaque,
                sortingCriteria = SortingCriteria.CommonOpaque,
            };

            RendererList rendererList = context.CreateRendererList(desc);

            commandBuffer.DrawRendererList(rendererList);

            commandBuffer.EndSample("Forward Shading");
        }

        {
            commandBuffer.BeginSample("Sky Rendering");
            RendererList skyRendererList = context.CreateSkyboxRendererList(camera);
            commandBuffer.DrawRendererList(skyRendererList);
            commandBuffer.EndSample("Sky Rendering");
        }

        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Release();

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
#endif

        context.DrawUIOverlay(camera);

        context.Submit();
    }
}
