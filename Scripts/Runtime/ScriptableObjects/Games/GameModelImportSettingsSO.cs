using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameModelImportSettings", menuName = "HoyoToon/GameModelImportSettings")]
    public class GameModelImportSettingsSO : GameScopedScriptableObject
    {
        [SerializeField] private GameModelImportDefaultsData defaults = new GameModelImportDefaultsData();

        public GameModelImportDefaultsData Defaults => defaults;
    }
}