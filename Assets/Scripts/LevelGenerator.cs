using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class LevelGenerator : MonoBehaviour
{
    [Header("Level parameters")]
    [SerializeField] private int levelWidth = 28;
    [SerializeField] private int levelHeight = 30;

    [Header("Level tiles")]
    [SerializeField] private Tile[] backgrounds;
    [SerializeField] private Tile[] obstacle;
    [SerializeField] private Tile[] levelBorder;

    [Header("Prefabs")] 
    [SerializeField] private GameObject tilemap;
    [SerializeField] private Material tilemapMaterial;
    
    private Tilemap _background;
    private Tilemap _foreground;
    private char[,] _levelTiles;
    
    // Up, Right, Down, Left moves
    private readonly Vector2Int[] _moves =
        {new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)};
    private readonly Tuple<int, int, Vector2Int>[] _wallBlueprints =
    {
        // Horizontal bars
        new Tuple<int, int, Vector2Int>(3, 2, new Vector2Int(1, 0)),
        new Tuple<int, int, Vector2Int>(5, 2, new Vector2Int(2, 0)),
        new Tuple<int, int, Vector2Int>(7, 2, new Vector2Int(3, 0)),
        // Vertical bars
        new Tuple<int, int, Vector2Int>(2, 3, new Vector2Int(0, 1)),
        new Tuple<int, int, Vector2Int>(2, 5, new Vector2Int(0, 2)),
        new Tuple<int, int, Vector2Int>(2, 7, new Vector2Int(0, 3)),
        // Rectangles
        new Tuple<int, int, Vector2Int>(3, 5, new Vector2Int(1, 2)),
        new Tuple<int, int, Vector2Int>(5, 3, new Vector2Int(2, 1)),
        new Tuple<int, int, Vector2Int>(5, 5, new Vector2Int(2, 2)),
    };

    
    public void GenerateLevel()
    {
        for (int i = 0; i < levelHeight; ++i)
        {
            for (int j = 0; j < levelWidth; ++j)
            {
                _levelTiles[i, j] = '.';
            }
        }
        var diggerPos = new Vector2Int(0, 0);

        bool canBuild = true, canMove = true;
        while (canBuild || canMove)
        {
            canBuild = TryBuild(diggerPos);
            canMove = TryDigCorridor(diggerPos);
        }
    }

    private bool TryBuild(Vector2Int diggerPos)
    {
        List<int> shuffledIdx = Enumerable.Range(0, _wallBlueprints.Length).ToList();
        RandomShuffle(shuffledIdx);
        foreach (var idx in shuffledIdx)
        {
            var blueprint = _wallBlueprints[idx];
            int width = blueprint.Item1;
            int height = blueprint.Item2;
            Vector2Int centerPos = blueprint.Item3;
            if (CanBuild(width, height, diggerPos - centerPos))
            {
                Build(width, height, diggerPos - centerPos, 'W');
                return true; // Return true if managed to build any wall type
            }
        }
        
        return false; // Return false if failed to build any wall type
    }

    private bool TryDigCorridor(Vector2Int diggerPos)
    {
        return false;
    }

    private bool CanBuild(int width, int height, Vector2Int startingPos)
    {
        for (int i = startingPos[1]; i < startingPos[1] + height; ++i)
        {
            for (int j = startingPos[0]; j < startingPos[0] + width; ++j)
            {
                if (_levelTiles[i, j] != '.')
                {
                    return false;
                }
            }
        }

        return true;
    }
    
    private void Build(int width, int height, Vector2Int startingPos, char building)
    {
        for (int i = startingPos[1]; i < startingPos[1] + height; ++i)
        {
            for (int j = startingPos[0]; j < startingPos[0] + width; ++j)
            {
                _levelTiles[i, j] = building;
            }
        }
    }

    private void RandomShuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++) {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
    
    void Start()
    {
        var grid = FindObjectOfType<Grid>();
        _background = Instantiate(tilemap, grid.transform).GetComponent<Tilemap>();
        _foreground = Instantiate(tilemap, grid.transform).GetComponent<Tilemap>();
        InitTilemap(_background, "Background");
        InitTilemap(_foreground, "Foreground");
        _levelTiles = new char[levelHeight, levelWidth];
        
        GenerateLevel();
    }

    private void InitTilemap(Tilemap tilemap, String name)
    {
        tilemap.name = name;
        var renderer = tilemap.GetComponent<TilemapRenderer>();
        renderer.material = tilemapMaterial;
        renderer.sortingLayerName = name;
    }

    private enum TileDirection : int
    {
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left,
        Middle,
    }

    private enum MoveDirection : int
    {
        Up,
        Right,
        Down,
        Left,
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
