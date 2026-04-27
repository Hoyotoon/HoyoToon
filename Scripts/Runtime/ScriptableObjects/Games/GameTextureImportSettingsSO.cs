using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameTextureImportSettings", menuName = "HoyoToon/GameTextureImportSettings")]
    public class GameTextureImportSettingsSO : GameScopedScriptableObject
    {
        [SerializeField] private GameTextureImportRuleData defaults = new GameTextureImportRuleData();
        [SerializeField] private GameTextureImportRuleMapData nameEquals = new GameTextureImportRuleMapData();
        [SerializeField] private GameTextureImportRuleMapData nameContains = new GameTextureImportRuleMapData();
        [SerializeField] private GameTextureImportRuleMapData nameEndsWith = new GameTextureImportRuleMapData();

        public GameTextureImportRuleData Defaults => defaults;

        public GameTextureImportRuleMapData NameEquals => nameEquals;

        public GameTextureImportRuleMapData NameContains => nameContains;

        public GameTextureImportRuleMapData NameEndsWith => nameEndsWith;
    }
}