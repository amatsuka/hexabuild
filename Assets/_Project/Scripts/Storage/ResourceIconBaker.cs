using System.Collections.Generic;
using Game.Economy;
using UnityEngine;

namespace Game.Storage
{
    /// <summary>
    /// Снимок модели ресурса для канваса. Склад — Screen Space Overlay, трёхмерный объект туда
    /// не вставить; поэтому каждая модель один раз рендерится в текстуру, а клетка показывает
    /// её. Так на дороге и в клетке лежит один и тот же предмет, чего требует спека.
    /// </summary>
    public sealed class ResourceIconBaker
    {
        /// <summary>
        /// Сцена снимка стоит далеко под полем: дальность теней 50 юнитов, значит на модель
        /// не упадёт ничего лишнего, а поле не попадёт в кадр — и всё это без своего слоя.
        /// </summary>
        static readonly Vector3 Stage = new(0f, -1000f, 0f);

        readonly Dictionary<ResourceType, RenderTexture> baked = new();
        readonly ResourceModels models;
        readonly int resolution;
        readonly Quaternion view;
        readonly float margin;

        bool ready;

        public ResourceIconBaker(ResourceModels models, int resolution, Vector3 viewAngles, float margin)
        {
            this.models = models;
            this.resolution = resolution;
            this.margin = margin;
            view = Quaternion.Euler(viewAngles);
        }

        /// <summary>Снимок модели ресурса или null, если модели нет и рисовать надо полигоном.</summary>
        public Texture Get(ResourceType type)
        {
            if (!ready)
                Bake();

            return baked.TryGetValue(type, out var texture) ? texture : null;
        }

        public void Dispose()
        {
            foreach (var texture in baked.Values)
                if (texture != null)
                    texture.Release();

            baked.Clear();
            ready = false;
        }

        void Bake()
        {
            ready = true;
            if (models == null || models.Material == null)
                return;

            var stage = new GameObject("ResourceIconStage", typeof(MeshFilter), typeof(MeshRenderer));
            stage.hideFlags = HideFlags.HideAndDontSave;
            stage.transform.position = Stage;
            stage.transform.rotation = view;

            var stageRenderer = stage.GetComponent<MeshRenderer>();
            stageRenderer.sharedMaterial = models.Material;
            stageRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stageRenderer.receiveShadows = false;

            var cameraObject = new GameObject("ResourceIconCamera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 0.5f * margin;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Прозрачный фон: шейдер пишет альфу 1 на самой модели, вокруг остаётся ноль.
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cameraObject.transform.position = Stage - Vector3.forward * 5f;
            cameraObject.transform.rotation = Quaternion.identity;

            var filter = stage.GetComponent<MeshFilter>();
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                var model = models.Get(type);
                if (model == null)
                    continue;

                var scale = ResourceModels.ScaleFor(model, 1f);
                filter.sharedMesh = model;
                stage.transform.localScale = Vector3.one * scale;
                // Пивот у моделей разный: бревно центрировано, кирпич стоит на основании.
                // Ведём кадр по центру габаритов, а не по пивоту.
                stage.transform.position = Stage - view * (model.bounds.center * scale);

                var texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                texture.name = $"{type} icon";
                camera.targetTexture = texture;
                camera.Render();
                camera.targetTexture = null;
                baked[type] = texture;
            }

            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
