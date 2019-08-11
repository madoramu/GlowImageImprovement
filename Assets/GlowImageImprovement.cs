namespace UnityEngine.UI
{
    // ImageにアタッチされているSpriteにアウトラインを描画するスクリプト
    // UIのImageを継承して使用するのでUnityEngine.UIの中に記載している
    public class GlowImageImprovement : Image
    {
        [SerializeField, Range(0, 10), Tooltip("アウトラインの太さ")]
        private float m_OutlineSize = 2.0f;
        public float outlineSize
        {
            get{ return m_OutlineSize; }
            set
            {
                if(Mathf.Approximately(m_OutlineSize, value)) return;
                m_OutlineSize = value;
                SetMaterialDirty();
            }
        }

        [SerializeField, Tooltip("アウトラインの色")]
        private Color m_OutlineColor = Color.white;
        public Color outlineColor
        {
            get{ return m_OutlineColor; }
            set
            {
                if(m_OutlineColor == value) return;
                m_OutlineColor = value;
                SetMaterialDirty();
            }
        }

        [SerializeField, Range(0, 10), Tooltip("アウトラインの強さ")]
        private float m_OutlineStrength　= 1.0f;
        public float outlineStrength
        {
            get{ return m_OutlineStrength; }
            set
            {
                if(Mathf.Approximately(m_OutlineStrength, value)) return;
                m_OutlineStrength = value;
                SetMaterialDirty();
            }
        }

        public enum ImageOutlineQuality
        {
            Low = 3,
            Medium = 5,
            High = 7
        }

        [SerializeField, Tooltip("線のクオリティ")]
        private ImageOutlineQuality m_Quality = ImageOutlineQuality.Medium;
        public ImageOutlineQuality quality
        {
            get{ return m_Quality; }
            set
            {
                if(m_Quality == value) return;
                m_Quality = value;
                SetMaterialDirty();
            }
        }

        // CanvasRendererに送られるマテリアル
        // 基本的にアタッチされた際に発火される
        // マテリアルの生成と自分のメンバ変数にマテリアルを格納するために使用する
        public override Material materialForRendering
        {
            get
            {
                if(m_Material == null)
                {
                    material = new Material(Shader.Find("UI/GlowImageImprovement"));
                }
                // ここでパラメーター設定行う
                material.SetFloat("_OutlineSize", m_OutlineSize);
                material.SetColor("_OutlineColor", m_OutlineColor);
                material.SetFloat("_OutlineStrength", m_OutlineStrength);
                string format = "QUALITY_{0}";
                foreach (var item in System.Enum.GetNames(typeof(ImageOutlineQuality)))
                {
                    material.DisableKeyword(string.Format(format, item.ToUpper()));
                }
                material.EnableKeyword(string.Format(format, quality.ToString().ToUpper()));

                return base.materialForRendering;
            }
        }

        // UIのメッシュ作成時に呼ばれるコールバック関数
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            // Simple以外、もしくはSpriteが設定されていない場合は通常のImage処理を行って抜ける
            if(type != Type.Simple || overrideSprite == null)
            {
                base.OnPopulateMesh(toFill);
                return;
            }

            // Spriteの上下左右の余白を削った時の値を取得する
            // x...左, y...下, z...右, w...上
            Vector4 padding = Sprites.DataUtility.GetPadding(overrideSprite);
            // spriteの整数値の縦横を取得
            Vector2Int spriteSize = new Vector2Int(Mathf.RoundToInt(overrideSprite.rect.width), Mathf.RoundToInt(overrideSprite.rect.height));
            // 現在のRectTransformの縦横とその右上コーナー座標を返す
            Rect rect = GetPixelAdjustedRect();
            // 左下右上それぞれのパディングの比率を出す
            // 左右(padding.x / spriteSize.x : (spriteSize.x - padding.z) / spriteSize.x)
            // 下上(padding.y / spriteSize.y : (spriteSize.y - padding.w) / spriteSize.y)
            Vector4 paddingRatio = new Vector4(
                padding.x / spriteSize.x, 
                padding.y / spriteSize.y, 
                (spriteSize.x - padding.z) / spriteSize.x, 
                (spriteSize.y - padding.w) / spriteSize.y);
 
            // アスペクト比維持の場合は、ここでrectの縦横を正方形に直して
            // 右上コーナー座標を修正する
            if(preserveAspect && spriteSize.sqrMagnitude > 0.0f)
            {
                float spriteRatio = (float)spriteSize.x / (float)spriteSize.y;
                float rectRatio = rect.width / rect.height;
                if(spriteRatio > rectRatio)
                {
                    float oldHeight = rect.height;
                    rect.height = rect.width * (1.0f / spriteRatio);
                    rect.y += (oldHeight - rect.height) * rectTransform.pivot.y;
                }
                else
                {
                    float oldWidth = rect.width;
                    rect.width = rect.height * (1.0f / spriteRatio);
                    rect.x += (oldWidth - rect.width) * rectTransform.pivot.x;
                }
            }

            // 現在のRectTransformの左下座標と右上座標を格納している
            Vector4 rectMinMax = new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
            Vector2 rectMinMaxSize = new Vector2(rectMinMax.z - rectMinMax.x, rectMinMax.w - rectMinMax.y);
            // 余白領域を切り取った時のrect情報
            Vector4 rectClipMinMax = new Vector4(
                rect.x + rect.width * paddingRatio.x, 
                rect.y + rect.height * paddingRatio.y,
                rect.x + rect.width * paddingRatio.z,
                rect.y + rect.height * paddingRatio.w);
            
            // 引数のSpriteのUV情報を取得するx,yが最小値、z,wが最大値
            // ここで渡される値も余白は削った値となっている
            Vector4 uvClip = Sprites.DataUtility.GetOuterUV(overrideSprite);

            // pixelsPerUnit...ワールド空間の1単位分のpixel数
            // Imageのgetではspriteの値をcanvasで割った値が返される
            // 1.0fが返されれば100%の拡大率で描画され、0.5や1.5の場合は
            // 50%、150%の拡大率で描画される
            // ピクセルの拡大率を1/Nという形にしたもの
            float unitPerPixel = 1.0f / pixelsPerUnit;
            Vector2 pixelFix = new Vector2(unitPerPixel / rectMinMaxSize.x, unitPerPixel / rectMinMaxSize.y);
            uvClip += new Vector4(pixelFix.x, pixelFix.y, -pixelFix.x, -pixelFix.y);

            // アウトラインの実サイズ
            float os = (outlineSize + (int)quality * 2) * unitPerPixel;
            // 余白を除いた部分とアウトライン分を引いた時の差分を出す
            Vector4 vectorOS = new Vector4(-os, -os, os, os);
            Vector4 diff = rectMinMax - rectClipMinMax - vectorOS;
            
            // UV座標の基準値
            Vector4 uvPosition = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);

            // 差分の補正(アウトラインを追加した分既存のRecttransformからはみ出る場合があるため)
            if(diff.x > 0.0f)
            {
                rectMinMax.x -= diff.x;
                uvPosition.x -= diff.x / rectMinMaxSize.x;
            }
            if(diff.y > 0.0f)
            {
                rectMinMax.y -= diff.y;
                uvPosition.y -= diff.y / rectMinMaxSize.y;
            }
            if(diff.z < 0.0f)
            {
                rectMinMax.z -= diff.z;
                uvPosition.z -= diff.z / rectMinMaxSize.x;
            }
            if(diff.w < 0.0f)
            {
                rectMinMax.w -= diff.w;
                uvPosition.w -= diff.w / rectMinMaxSize.y;
            }
            
            /* メッシュを再生成する */ 
            // color→color32は暗黙変換してくれる
            //Color32 color32 = color;
            Color32 color32 = color;
            toFill.Clear();
            // 余白分を削ったRecttransformの座標を元に頂点設定(0~3)
            toFill.AddVert(new Vector3(rectClipMinMax.x, rectClipMinMax.y), color32, new Vector2(uvClip.x, uvClip.y)); // 左下
            toFill.AddVert(new Vector3(rectClipMinMax.x, rectClipMinMax.w), color32, new Vector2(uvClip.x, uvClip.w)); // 左上
            toFill.AddVert(new Vector3(rectClipMinMax.z, rectClipMinMax.w), color32, new Vector2(uvClip.z, uvClip.w)); // 右上
            toFill.AddVert(new Vector3(rectClipMinMax.z, rectClipMinMax.y), color32, new Vector2(uvClip.z, uvClip.y)); // 右下
            // 差分補正を掛けたRecttransformの座標を元に頂点設定(4~7)
            toFill.AddVert(new Vector3(rectMinMax.x, rectMinMax.y), color32, new Vector2(uvPosition.x, uvPosition.y)); // 左下
            toFill.AddVert(new Vector3(rectMinMax.x, rectMinMax.w), color32, new Vector2(uvPosition.x, uvPosition.w)); // 左上
            toFill.AddVert(new Vector3(rectMinMax.z, rectMinMax.w), color32, new Vector2(uvPosition.z, uvPosition.w)); // 右上
            toFill.AddVert(new Vector3(rectMinMax.z, rectMinMax.y), color32, new Vector2(uvPosition.z, uvPosition.y)); // 右下
            // rectClipMinMaxの矩形を囲う様に四角形を4枚作成する
            for(int i = 0; i < 4; ++i)
            {
                int n = i + 1 == 4 ? 0 : i + 1;
                toFill.AddTriangle(i + 4, n + 4, i);
                toFill.AddTriangle(n + 4, n, i);
            }
            // 最後にrectClipMinMaxの矩形を作成して終了
            toFill.AddTriangle(0, 1, 2);
            toFill.AddTriangle(2, 3, 0);
        }
    }
}
