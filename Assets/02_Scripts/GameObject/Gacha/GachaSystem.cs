using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class GachaSystem : MonoBehaviour
{
    private List<CharacterData> _allCharacterData = new List<CharacterData>();
    private List<CharacterData> _HigherCharacterData = new List<CharacterData>();

    public static GachaSystem Instance;

    private int _drawCharacterCount;
    private bool _isHigherGacha;
    private int _totalHigherGachaWeight;
    private int _totalGachaWeight;
    private bool _isCardDrawing;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
        SetVariable();
    }

    private void SetVariable()
    {
        _drawCharacterCount = 0;
        _isHigherGacha = false;
        _isCardDrawing = false;
    }

    private void GetAllCharacterData()
    {
        _allCharacterData = GameDataManager.Instance.GetAllData<CharacterData>();
        SetTotalGachaWeight();
    }

    private void SetTotalGachaWeight()
    {
        _totalGachaWeight = 0;

        foreach (var characterData in _allCharacterData)
        {
            _totalGachaWeight += characterData.GachaWeight;
        }
    }

    private void GetHigherCharacterData()
    {
        if (_allCharacterData == null || _allCharacterData.Count == 0)
        {
            _allCharacterData = GameDataManager.Instance.GetAllData<CharacterData>();
        }

        _HigherCharacterData.Clear();

        string LowRarity = "C";

        foreach (var characterData in _allCharacterData)
        {
            if (characterData.Rarity != LowRarity)
            {
                _HigherCharacterData.Add(characterData);
            }
        }

        SetHigherTotalGachaWeight();
    }

    private void SetHigherTotalGachaWeight()
    {
        _totalHigherGachaWeight = 0;

        foreach (var characterData in _HigherCharacterData)
        {
            _totalHigherGachaWeight += characterData.GachaWeight;
        }
    }

    public async UniTask<CharacterData> DrawSingleCharacter()
    {
        bool _isDiamondExist = NetworkManager.Instance.PlayerResourceService.RequestUseDiamond(100);
        if (_isDiamondExist == false)
        {
            Debug.LogError($"다이아몬드가 부족합니다. 현재 다이아 갯수: {SaveManager.Instance.CurrentSaveData.Diamond}");
            return null;
        }
        if (_isCardDrawing == true)
        {
            NetworkManager.Instance.PlayerResourceService.RequestAddDiamond(100);
            return null;
        }

        _isCardDrawing = true;

        try
        {
            PrepareGachaData();

            if (_allCharacterData == null || _allCharacterData.Count == 0)
            {
                Debug.LogError("[GachaSystem] 캐릭터 데이터를 불러오지 못했습니다.");
                return null;
            }

            CheckDrawHigherCharacters();

            int totalGachaWeight;

            if (_isHigherGacha)
            {
                totalGachaWeight = _totalHigherGachaWeight;
            }
            else
            {
                totalGachaWeight = _totalGachaWeight;
            }

            int randomValue = DrawRandomValue(totalGachaWeight);

            CharacterData character = DrawCharacter(randomValue);

            if (character == null)
                return null;

            await ShowSingleGachaResult(character);

            return character;
        }
        finally
        {
            _isCardDrawing = false;
        }
    }

    public async UniTask<List<CharacterData>> DrawMultipleCharacter(int count = 10)
    {
        bool _isDiamondExist = NetworkManager.Instance.PlayerResourceService.RequestUseDiamond(1000);
        if (_isDiamondExist == false)
        {
            Debug.LogError($"다이아몬드가 부족합니다. 현재 다이아 갯수: {SaveManager.Instance.CurrentSaveData.Diamond}");
            return null;
        }
        if (_isCardDrawing == true)
        {
            NetworkManager.Instance.PlayerResourceService.RequestAddDiamond(1000);
            return null;
        }

        _isCardDrawing = true;

        List<CharacterData> characters = new List<CharacterData>();

        try
        {
            PrepareGachaData();

            for (int i = 0; i < count; i++)
            {
                CheckDrawHigherCharacters();

                int totalGachaWeight;

                if (_isHigherGacha == true)
                {
                    totalGachaWeight = _totalHigherGachaWeight;
                }
                else
                {
                    totalGachaWeight = _totalGachaWeight;
                }

                int randomValue = DrawRandomValue(totalGachaWeight);

                var drawnCharacter = DrawCharacter(randomValue);

                if (drawnCharacter != null)
                {
                    characters.Add(drawnCharacter);
                }
            }
            Debug.Log($"10연차 결과: {characters.Count}개");

            await ShowMultipleGachaResult(characters);

            return characters;
        }

        finally
        {
            _isCardDrawing = false;
        }
    }

    private int DrawRandomValue(int totalWeight)
    {
        return Random.Range(0, totalWeight);
    }

    private CharacterData DrawCharacter(int randomValue)
    {
        int currentWeightSum = 0;

        if (_isHigherGacha == true)
        {
            foreach (var character in _HigherCharacterData)
            {
                currentWeightSum += character.GachaWeight;
                if (randomValue < currentWeightSum)
                {
                    return DrawnCharacter(currentWeightSum, randomValue, character);
                }
            }
        }

        else
        {
            foreach (var character in _allCharacterData)
            {
                currentWeightSum += character.GachaWeight;
                if (randomValue < currentWeightSum)
                {
                    return DrawnCharacter(currentWeightSum, randomValue, character);
                }
            }
        }

        return null;
    }

    private void PrepareGachaData()
    {
        if (_allCharacterData == null || _allCharacterData.Count == 0)
        {
            GetAllCharacterData();
        }

        if (_HigherCharacterData == null || _HigherCharacterData.Count == 0)
        {
            GetHigherCharacterData();
        }
    }

    private CharacterData DrawnCharacter(int currentWeightSum, int randomValue, CharacterData character)
    {
        LogDrawnCharacter(character);

        OpenCharacterCardUI(character);

        // 실제로 저장되어야 하므로 CharacterUtils에서 Add를 통하여 인스턴스 Id를 가지며 인벤토리에 들어가도록 추가
        HunterUtil hunterUtil = new HunterUtil();
        hunterUtil.AddCharacters(character.Id);

        _drawCharacterCount++;
        Debug.Log($"{_drawCharacterCount}개의 캐릭터를 뽑았습니다.");

        return character;
    }

    private async UniTask ShowSingleGachaResult(CharacterData character)
    {
        GachaResultUI gachaResultUI = await UiManager.Instance.OpenUi<GachaResultUI>();

        await gachaResultUI.SetSingleGachaResult(character);

        gachaResultUI.Button_CloseScreen.gameObject.SetActive(true);
    }
    private async UniTask ShowMultipleGachaResult(List<CharacterData> characters)
    {
        GachaResultUI gachaResultUI = await UiManager.Instance.OpenUi<GachaResultUI>();

        await gachaResultUI.SetMultipleGachaResult(characters);

        gachaResultUI.Button_CloseScreen.gameObject.SetActive(true);
    }

    private void OpenCharacterCardUI(CharacterData character)
    {
        //[TODO] 카드 UI 오픈
        string id = character.Id;
        int starLevel = character.StarLevel;
        string name = character.Name;
        string rarity = character.Rarity;
        var baseStatData = GameDataManager.Instance.GetData<BaseStatData>(character.BaseStatDataId);
        int baseAtk = baseStatData.BaseAtk;
        int baseDef = baseStatData.BaseDef;
        int baseAtkSpeed = baseStatData.BaseAtkSpeed;
        int baseHp = baseStatData.BaseHp;
    }

    private void LogDrawnCharacter(CharacterData character)
    {
        string prefix = _isHigherGacha ? "[최소 등급 보장] " : "";
        Debug.Log($"{prefix}뽑힌 캐릭터: {character.Id}, 등급: {character.Rarity}");
    }

    private void CheckDrawHigherCharacters()
    {
        if (_drawCharacterCount % 10 == 9)
        {
            _isHigherGacha = true;
            return;
        }

        _isHigherGacha = false;
    }
}
