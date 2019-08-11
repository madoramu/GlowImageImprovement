// uGUIのImageシェーダーを改造したもの
// uGUIのImageに設定されているSpriteにGlowアウトラインを追加するシェーダー
// ※Imageの設定がSimpleのみ対応している
Shader "UI/GlowImageImprovement"
{
	Properties
	{
		[PerRendererData] _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        /* アウトライン用に追加したプロパティ要素 */
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineSize ("Outline Size", float) = 1
        _OutlineStrength("Outline Strength", float) = 1
	}

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "UnityUI.cginc"

    struct appdata_t
    {
        float4 vertex   : POSITION;
        float4 color    : COLOR;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    // 通常Imageのフラグメント用情報
    struct v2f
    {
        float4 vertex   : SV_POSITION;
        fixed4 color    : COLOR;
        float2 texcoord  : TEXCOORD0;
        float4 worldPosition : TEXCOORD2;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    // アウトラインのフラグメント用情報
    struct v2of
    {
        float4 vertex   : SV_POSITION;
        fixed4 color    : COLOR;
        float4 worldPosition : TEXCOORD0;
        half2 coordV : TEXCOORD1;
        half2 coordH : TEXCOORD2;
        half2 offsetV: TEXCOORD3;
        half2 offsetH: TEXCOORD4;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    #pragma multi_compile __ UNITY_UI_ALPHACLIP
    #pragma multi_compile __ UNITY_UI_CLIP_RECT
    #pragma multi_compile QUALITY_LOW QUALITY_MEDIUM QUALITY_HIGH

    // ガウシアンフィルタ適用時のパラメーター
    // 3x3
    #ifdef QUALITY_LOW
    static const float weights[2] = { 0.4518628, 0.2740686 };
    static const int sampling = 1;
    #endif
    // 5x5
    #ifdef QUALITY_MEDIUM
    static const float weights[3] = { 0.40262, 0.2442014, 0.05448869 };
    static const int sampling = 2;
    #endif
    // 9x9
    #ifdef QUALITY_HIGH
    static const float weights[5] = { 0.3989435, 0.2419714, 0.05399112, 0.004431861, 0.0001338306 };
    static const int sampling = 4;
    #endif

    fixed4 _Color;
    fixed4 _TextureSampleAdd;
    float4 _ClipRect;
    sampler2D _MainTex;
    float4 _MainTex_ST;
    float4 _MainTex_TexelSize;
    fixed4 _OutlineColor;
    float _OutlineSize;
    float _OutlineStrength;

    // 通常Imageのバーテクスシェーダー
    v2f vert(appdata_t v)
    {
        v2f OUT;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
        OUT.worldPosition = v.vertex;
        OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
        OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
        OUT.color = v.color * _Color;
        return OUT;
    }

    // アウトライン用に情報追加したもの
    v2of vert_outline (appdata_t v)
    {
        v2of OUT;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
        OUT.worldPosition = v.vertex;
        OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
        OUT.color = v.color * _Color;

        half2 uv = TRANSFORM_TEX(v.texcoord, _MainTex);
        // サンプリングポイントのオフセット
        // _MainTex_TexelSize.xy = (1/width, 1/height)となっている
        OUT.offsetV = _MainTex_TexelSize.xy * half2(0.0, 1.0) * _OutlineSize;
        OUT.offsetH = _MainTex_TexelSize.xy * half2(1.0, 0.0) * _OutlineSize;
        // サンプリング開始ポイントのUV座標
        OUT.coordV = uv - OUT.offsetV * sampling;
        OUT.coordH = uv - OUT.offsetH * sampling;
        return OUT;
    }

    // 現在のUV座標が0未満1越えの場合は完全に透明にする
    #define ClampSprite(alpha, texcoord) (alpha * step(0, texcoord.x) * step(0, texcoord.y) * step(texcoord.x, 1) * step(texcoord.y, 1))

    // 通常のImageフラグメントシェーダー。Spriteを描画する
    // 但し元のSpriteの範囲外の物はClampする
    fixed4 frag(v2f IN) : SV_Target
    {
        half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

        #ifdef UNITY_UI_CLIP_RECT
        color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
        #endif

        #ifdef UNITY_UI_ALPHACLIP
        clip (color.a - 0.001);
        #endif

        // アウトライン分テクスチャが引き延ばされているため
        // UV座標が0未満1越えになっている個所があるのでそこを削る
        color.a = ClampSprite(color.a, IN.texcoord);

        return color;
    }

    // アウトライン描画用フラグメントシェーダー
    fixed4 frag_outline (v2of IN) : SV_Target
    {
        // Clip処理は先に済ませる
        #ifdef UNITY_UI_CLIP_RECT
        color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
        #endif
        #ifdef UNITY_UI_ALPHACLIP
        clip (color.a - 0.001);
        #endif

        // アウトラインをぼかすためのサンプリング処理を行う
        // 重みを掛ける部分で0.5をかけているのは、水平方向と垂直方向の結果を合計するため
        float outlineAlpha = 0;
        float tempAlpha;
        // 水平方向
        for(int i = -sampling; i <= sampling; ++i)
        {
            // 指定画素のアルファ値に重みを掛けた値を格納
            tempAlpha = (tex2D(_MainTex, IN.coordV).a + _TextureSampleAdd.a) * weights[abs(i)] * 0.5;
            // 指定画素がテクスチャ外の場合は0にする
            outlineAlpha += ClampSprite(tempAlpha, IN.coordV);
            IN.coordV += IN.offsetV;
        }
        // 垂直方向
        for(int j = -sampling; j <= sampling; ++j)
        {
            // 指定画素のアルファ値に重みを掛けた値を格納
            tempAlpha = (tex2D(_MainTex, IN.coordH).a + _TextureSampleAdd.a) * weights[abs(j)] * 0.5;
            // 指定画素がテクスチャ外の場合は0にする
            outlineAlpha += ClampSprite(tempAlpha, IN.coordH);
            IN.coordH += IN.offsetH;
        }

        // サンプリングして算出したアルファ値を元に色の設定とクリッピングを行う
        half4 outlineColor = _OutlineColor;
        outlineColor.a *= outlineAlpha * IN.color.a;
        #ifdef UNITY_UI_ALPHACLIP
        clip (outlineColor.a - 0.001);
        #endif
        outlineColor *= _OutlineStrength;
        return outlineColor;
    }
    ENDCG

	SubShader
	{
		Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        // アウトラインを先に描画
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_outline
			#pragma fragment frag_outline
            #pragma target 2.0
			ENDCG
		}
        // その後に元画像を描画
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            ENDCG
        }
	}
}
