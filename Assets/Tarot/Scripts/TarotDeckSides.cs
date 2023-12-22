using UnityEngine;

namespace PastInfinity
{
    public class TarotDeckSides : MonoBehaviour
    {

        public bool isLongSide = false;

        private int catalogCardIndex = 160;

        //These store the original and split mesh
        public Mesh change { get; set; } = null;

        const int xTiles = 24;
        const int yTiles = 7;
        const float scaleX = 1.0f / xTiles;

        // const float scaleY = 1.0f / yTiles;

        // For perfect 4096x4096 texture:
        const float scaleY = (13720.0f / 13848.0f) / yTiles;
        const float offsetY = 128.0f / 13848.0f;

        // Use this for initialization, before relativistic object CombineParent() starts.
        void Awake()
        {
            //Grab the meshfilter, and if it's not null, keep going
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            int xIndex = catalogCardIndex % xTiles;
            int yIndex = (yTiles - 1) - (catalogCardIndex / xTiles);

            //Prepare a new mesh for our split mesh
            change = Instantiate(meshFilter.mesh);

            Vector2 scale = new Vector2(scaleX, scaleY);
            float offsetX = (isLongSide ? 1 : 0) * scaleX;

            Vector2[] nUv = new Vector2[change.uv.Length];
            for (int uvIndex = 0; uvIndex < nUv.Length; uvIndex++)
            {
                // nUv[uvIndex] = Vector2.Scale(scale, change.uv[uvIndex]) + new Vector2(scaleX * xIndex + offsetX, scaleY * yIndex);
                // For perfect 4096x4096 texture:
                nUv[uvIndex] = Vector2.Scale(scale, change.uv[uvIndex]) + new Vector2(scaleX * xIndex + offsetX, scaleY * yIndex + offsetY);
            }

            change.uv = nUv;
            meshFilter.mesh = change;
        }
    }
}