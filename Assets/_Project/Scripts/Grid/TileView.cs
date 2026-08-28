using UnityEngine;

namespace Game.Grid
{
    /// <summary>Визуал одной плитки: меш гекса и цвет по состоянию.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Color hiddenColor = new(0.18f, 0.18f, 0.20f);
        [SerializeField] Color metropolisColor = new(0.20f, 0.45f, 0.85f);

        MeshRenderer meshRenderer;
        MaterialPropertyBlock propertyBlock;

        public HexCoord Coord { get; private set; }

        public void Bind(TileData tile)
        {
            Coord = tile.Coord;
            name = $"Hex {tile.Coord}";
            transform.localPosition = tile.Coord.ToWorld();
            GetComponent<MeshFilter>().sharedMesh = HexMeshBuilder.Shared;

            if (tile.IsMetropolis)
                ShowMetropolis();
            else
                ShowHidden();
        }

        public void ShowHidden() => SetColor(hiddenColor);

        public void ShowMetropolis() => SetColor(metropolisColor);

        void SetColor(Color color)
        {
            meshRenderer ??= GetComponent<MeshRenderer>();
            propertyBlock ??= new MaterialPropertyBlock();

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
