using UnityEngine;

namespace ArrangeRenderer
{
    /// <summary>
    /// 1 枚のテクスチャを、バラバラにして、並び替えて出力します。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public class ArrangeRenderer : MonoBehaviour
    {
        #region Enum

        /// <summary>
        /// 描画モード。
        /// </summary>
        public enum RenderMode
        {
            /// <summary>
            /// 標準的な描画モード。
            /// </summary>
            Default,

            /// <summary>
            /// 任意に整列されたモード。
            /// </summary>
            Arranged
        }

        #endregion Enum

        #region Field

        /// <summary>
        /// 描画する Texture。
        /// </summary>
        public Texture texture;

        /// <summary>
        /// 各矩形の描画に利用するマテリアル。
        /// 通常は Unlit/Texture, 透過が必要なら Unlit/Transparent シェーダのマテリアルを適用するなどします。
        /// </summary>
        public Material drawRectMaterial;

        /// <summary>
        /// 描画モード。
        /// </summary>
        public RenderMode renderMode;

        /// <summary>
        /// 描画する領域。原点を左下として定義します。
        /// </summary>
        public Rect[] viewportRectsInUvSpace;

        /// <summary>
        /// 描画領域に描画するテクスチャの座標を示したデータ。原点を左下として定義します。
        /// </summary>
        public Rect[] uvRectsInUvSpace;

        /// <summary>
        /// テクスチャを描画する矩形の頂点。
        /// </summary>
        protected static Vector2[] DefaultQuadPoints = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(0, 1),new Vector2(1, 1), new Vector2(1, 0)
        };

        #endregion Field

        #region Method

        /// <summary>
        /// 描画時に呼び出されます。
        /// </summary>
        /// <param name="source">
        /// 描画前に入力される RenderTexture 。
        /// </param>
        /// <param name="destination">
        /// 描画後に出力される RenderTexture 。
        /// </param>
        protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // (1) 出力サイズを取得します。

            Vector2 destinationScale;

            if (destination == null)
            {
                destinationScale = new Vector2(Screen.width, Screen.height);
            }
            else
            {
                destinationScale = new Vector2(destination.width, destination.height);
            }

            // (2) 描画モードを確定します。

            RenderMode renderMode = this.renderMode;

            int viewportRectsInUvSpaceLength = this.viewportRectsInUvSpace.Length;
            int uvRectsInUvSpaceLength       = this.uvRectsInUvSpace.Length;

            if (viewportRectsInUvSpaceLength != uvRectsInUvSpaceLength)
            {
                Debug.LogError("\"viewportRectsInUvSpace\" length must be same as \"uvRectsInUvSpace\" length");
                renderMode = RenderMode.Default;
            }

            // (3) 描画します。
            // 開始時の PushMatrix で任意の操作を加える前の変更を保存しておき、
            // 終了時の PopMatrix で変更した操作を元に( = PushMatrixで保存した状態)戻します。

            Graphics.SetRenderTarget(destination);
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix();
            GL.LoadOrtho();

            switch (renderMode)
            {
                case RenderMode.Default:
                    {
                        DrawRect(this.texture, new Rect(0, 0, destinationScale.x, destinationScale.y), ArrangeRenderer.DefaultQuadPoints);

                        break;
                    }
                case RenderMode.Arranged:
                    {
                        for (int i = 0; i < viewportRectsInUvSpaceLength; i++)
                        {
                            Rect viewportInScreenSpace = UvSpaceRectToScreenSpaceRect(this.viewportRectsInUvSpace[i],
                                                                                     (int)destinationScale.x,
                                                                                     (int)destinationScale.y);
                            Vector2[] uvmapInViewportSpace = ArrangeRenderer.RectToPoints(this.uvRectsInUvSpace[i]);

                            DrawRect(this.texture, viewportInScreenSpace, uvmapInViewportSpace);
                        }

                        break;
                    }
            }

            GL.PopMatrix();
        }

        /// <summary>
        /// テクスチャを任意の矩形を描画します。
        /// </summary>
        /// <param name="texture">
        /// 描画するテクスチャ。
        /// </param>
        /// <param name="viewportInScreenSpace">
        /// 出力画面の内のどの位置に描画するか(ビューポート)を表す Rect。
        /// 全画面に表示するときは、(0, 0, 1, 1) になります。
        /// </param>
        /// <param name="uvmapInViewportSpace">
        /// ビューポートに描画するテクスチャの UV 座標。
        /// テクスチャ全体を描画するときは (0, 0), (1, 0), (1,1), (1, 0) になります。
        /// </param>
        protected void DrawRect(Texture texture, Rect viewportInScreenSpace, Vector2[] uvmapInViewportSpace)
        {
            // ViewPort は出力先のいずれの領域に描画するかを示します。
            // GL 関数を使っていますが、時計回りの時に描画されるようです。
            // (一般的に、視線に対して DX は時計周り、GL は反時計回りでメッシュが表になる)
            // 
            // 座標系
            // ――――――――
            // |(0,1)    (1,1)|
            // |              |
            // |(0,0)    (1,0)|
            // ――――――――

            this.drawRectMaterial.mainTexture = texture;
            this.drawRectMaterial.SetPass(0);

            GL.Viewport(viewportInScreenSpace);

            GL.Begin(GL.QUADS);

            GL.Color(new Color(0, 0, 0, 0));

            GL.TexCoord(uvmapInViewportSpace[0]);
            GL.Vertex(ArrangeRenderer.DefaultQuadPoints[0]);

            GL.TexCoord(uvmapInViewportSpace[1]);
            GL.Vertex(ArrangeRenderer.DefaultQuadPoints[1]);

            GL.TexCoord(uvmapInViewportSpace[2]);
            GL.Vertex(ArrangeRenderer.DefaultQuadPoints[2]);

            GL.TexCoord(uvmapInViewportSpace[3]);
            GL.Vertex(ArrangeRenderer.DefaultQuadPoints[3]);

            GL.End();
        }

        /// <summary>
        /// 矩形の左下を原点として、時計回りに定義される 4 つの頂点を取得します。
        /// </summary>
        /// <returns>
        /// 左下を原点として時計回りに定義される 4 つの頂点。
        /// </returns>
        protected static Vector2[] RectToPoints(Rect rect)
        {
            Vector2[] points = new Vector2[4];

            points[0] = new Vector2(rect.x,              rect.y);
            points[1] = new Vector2(rect.x,              rect.y + rect.height);
            points[2] = new Vector2(rect.x + rect.width, rect.y + rect.height);
            points[3] = new Vector2(rect.x + rect.width, rect.y);

            return points;
        }

        /// <summary>
        /// UvSpace で定義された Rect を、ScreenSpace で定義された Rect に変換して取得します。
        /// </summary>
        /// <param name="rectInUvSpace">
        /// UvSpace で定義された Rect。
        /// </param>
        /// <param name="screenWidth">
        /// スクリーンの幅。
        /// </param>
        /// <param name="screenHeight">
        /// スクリーンの高さ。
        /// </param>
        /// <returns>
        /// ScreenSpace で定義された Rect。
        /// </returns>
        protected static Rect UvSpaceRectToScreenSpaceRect(Rect rectInUvSpace, int screenWidth, int screenHeight)
        {
            return new Rect()
            {
                x      = rectInUvSpace.x * screenWidth,
                y      = rectInUvSpace.y * screenHeight,
                width  = rectInUvSpace.width  * screenWidth,
                height = rectInUvSpace.height * screenHeight,
            };
        }

        #endregion Method
    }
}