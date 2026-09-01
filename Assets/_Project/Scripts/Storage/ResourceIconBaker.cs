using System.Collections.Generic;
using Game.Economy;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        readonly Dictionary<Mesh, RenderTexture> baked = new();
        readonly ResourceModels models;
        readonly int resolution;
        readonly Quaternion view;
        readonly float margin;

        GameObject stage;
        MeshFilter stageFilter;
        Camera stageCamera;

        public ResourceIconBaker(ResourceModels models, int resolution, Vector3 viewAngles, float margin)
        {
            this.models = models;
            this.resolution = resolution;
            this.margin = margin;
            view = Quaternion.Euler(viewAngles);
        }

        /// <summary>Снимок модели ресурса или null, если модели нет и рисовать надо полигоном.</summary>
        public Texture Get(ResourceType type) => Get(models.Get(type), models.Material, view);

        /// <summary>
        /// Снимок произвольной модели своим разворотом: так HUD берёт монету и свиток, которых
        /// в `ResourceType` нет. Печётся по требованию и запоминается по мешу.
        /// </summary>
        public Texture Get(Mesh model, Material material, Quaternion rotation)
        {
            if (model == null || material == null)
                return null;

            if (baked.TryGetValue(model, out var cached) && cached != null)
                return cached;

            EnsureStage();
            return baked[model] = Render(model, material, rotation);
        }

        public void Dispose()
        {
            foreach (var texture in baked.Values)
                if (texture != null)
                    texture.Release();

            baked.Clear();
            DestroyStage();
        }

        /// <summary>Один снимок: модель ставится в кадр по центру своих габаритов и рендерится.</summary>
        RenderTexture Render(Mesh model, Material material, Quaternion rotation)
        {
            var scale = ResourceModels.ScaleFor(model, 1f);
            stageFilter.sharedMesh = model;
            stageFilter.GetComponent<MeshRenderer>().sharedMaterial = material;
            stage.transform.rotation = rotation;
            stage.transform.localScale = Vector3.one * scale;
            // Пивот у моделей разный: бревно центрировано, кирпич стоит на основании.
            // Ведём кадр по центру габаритов, а не по пивоту.
            stage.transform.position = Stage - rotation * (model.bounds.center * scale);

            var texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB) { name = model.name + " icon" };

            // Текстура чистится до кадра: свежая `RenderTexture` содержит мусор видеопамяти, и
            // если рендер по какой-то причине не состоится, иконка обязана выйти пустой, а не
            // раскрашенной чужим буфером. Ровно так веб-сборка и показывала кляксы.
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;

            // Кадр просится у самого конвейера, а не `Camera.Render()`: в SRP тот идёт мимо
            // конвейера, и в плеере отдаёт не то, что в редакторе. `StandardRequest` живёт
            // в ядре рендера, ссылки на пакет URP для него не нужно.
            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(stageCamera, request))
            {
                RenderPipeline.SubmitRenderRequest(stageCamera, request);
                return texture;
            }

            stageCamera.targetTexture = texture;
            stageCamera.Render();
            stageCamera.targetTexture = null;
            return texture;
        }

        void EnsureStage()
        {
            if (stage != null)
                return;

            stage = new GameObject("ResourceIconStage", typeof(MeshFilter), typeof(MeshRenderer))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            stage.transform.position = Stage;
            stageFilter = stage.GetComponent<MeshFilter>();

            var stageRenderer = stage.GetComponent<MeshRenderer>();
            stageRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stageRenderer.receiveShadows = false;

            var cameraObject = new GameObject("ResourceIconCamera", typeof(Camera))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            stageCamera = cameraObject.GetComponent<Camera>();
            stageCamera.enabled = false;
            stageCamera.orthographic = true;
            stageCamera.orthographicSize = 0.5f * margin;
            stageCamera.nearClipPlane = 0.01f;
            stageCamera.farClipPlane = 10f;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            // Прозрачный фон: шейдер пишет альфу 1 на самой модели, вокруг остаётся ноль.
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cameraObject.transform.position = Stage - Vector3.forward * 5f;
            cameraObject.transform.rotation = Quaternion.identity;

            // Камере снимка не нужно ничего из того, что конвейер делает ради кадра игры:
            // ни постобработки, ни теней, ни копий глубины и цвета. Каждый такой проход пишет
            // в свои цели, и в вебе именно они оставались в текстуре вместо самой модели —
            // иконки выходили кислотными. Модель освещена ключевым светом сцены, и этого хватает.
            var cameraData = stageCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
        }

        void DestroyStage()
        {
            if (stage == null)
                return;

            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(stageCamera.gameObject);
            stage = null;
            stageFilter = null;
            stageCamera = null;
        }
    }
}
