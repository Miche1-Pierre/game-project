using UnityEngine;

namespace TargetIndicators
{
    class ScreenData
    {
        readonly TargetIndicatorManager _targetIndicatorManager;

        public static Vector2 ScreenCenter => new(HalfScreenWidth, HalfScreenHeight);

        public static float HalfScreenWidth => Screen.width * 0.5f;

        public static float HalfScreenHeight => Screen.height * 0.5f;

        public Camera Camera => _targetIndicatorManager.Camera;

        public float LeftPadding => _targetIndicatorManager.LeftPadding;

        public float RightPadding => _targetIndicatorManager.RightPadding;

        public float TopPadding => _targetIndicatorManager.TopPadding;

        public float BottomPadding => _targetIndicatorManager.BottomPadding;

        public float Width => _targetIndicatorManager.Width;

        public float Height => _targetIndicatorManager.Height;

        public ScreenData(TargetIndicatorManager targetIndicatorManager)
        {
            _targetIndicatorManager = targetIndicatorManager;
        }
    }
}
