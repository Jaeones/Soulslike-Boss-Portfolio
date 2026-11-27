using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Define 
{

    // 태그와 레이어를 상수로 정의하여 오타 방지 및 관리 용이성 향상
    public struct Tags
    {
        public const string GROUND = "Ground";
        public const string ENEMY = "Enemy";
        //public const string ITEM = "Item";
        public const string PLAYER = "Player";
    }

    public struct Layers
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
        //public const string Item = "Item";
        public const string Ground = "Ground";
    }

    // 씬
    public enum Scene
    {
        Unknown,
        Title,
        Lobby,
        Game,
    }

    // 사운드
    public enum Sound
    {
        Bgm,
        Sfx,
        MaxCount,
    }

    // 마우스 이벤트

    public enum MouseEvent
    {
        Press,
        Drag,
        Click,
    }

    public enum CameraMode
    {
        QuarterView,
        ShoulderView,
    }

    // 메인 카메라 캐싱
    private static Camera _mainCamera;
    public static Camera MainCam
    {
        get
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            return _mainCamera;
        }
    }

}
