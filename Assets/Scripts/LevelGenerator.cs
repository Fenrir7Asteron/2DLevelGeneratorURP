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
    [SerializeField] private int halfWidthInTiles = 14;
    [SerializeField] private int halfHeightInTiles = 15;
    [SerializeField] [Range(0.4f, 1.0f)] private float maxEmptySpaces = 0.45f;

    [Header("Level tiles")]
    [SerializeField] private Tile[] obstacle;
    [SerializeField] private Tile[] levelBorder;

    [Header("Prefabs")] 
    [SerializeField] private GameObject tilemap;
    [SerializeField] private Material tilemapMaterial;
    
    [Header("References")] 
    [SerializeField] private Camera mainCamera;
    
    private Tilemap _foreground;
    private char[,] _levelTiles;
    
    // Up, Right, Down, Left moves
    private readonly Vector2Int[] _moves =
        {new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1)};
    private readonly Tuple<int, int, Vector2Int>[] _wallBlueprints =
    {
        // Horizontal bars
        new Tuple<int, int, Vector2Int>(3, 2, new Vector2Int(0, 1)),
        new Tuple<int, int, Vector2Int>(5, 2, new Vector2Int(0, 2)),
        new Tuple<int, int, Vector2Int>(7, 2, new Vector2Int(0, 3)),
        // Vertical bars
        new Tuple<int, int, Vector2Int>(2, 3, new Vector2Int(1, 0)),
        new Tuple<int, int, Vector2Int>(2, 5, new Vector2Int(2, 0)),
        new Tuple<int, int, Vector2Int>(2, 7, new Vector2Int(3, 0)),
        // Rectangles
        new Tuple<int, int, Vector2Int>(3, 5, new Vector2Int(2, 1)),
        new Tuple<int, int, Vector2Int>(5, 3, new Vector2Int(1, 2)),
        new Tuple<int, int, Vector2Int>(5, 5, new Vector2Int(2, 2)),
    };


    public void GenerateLevel()
    {
        while (true)
        {
            _foreground.ClearAllTiles();
            _levelTiles = new char[halfHeightInTiles, halfWidthInTiles];
            mainCamera.orthographicSize = Math.Max(halfHeightInTiles, (int) Math.Ceiling((double) halfWidthInTiles / 4 * 3)) + 3;
            for (int i = 0; i < halfHeightInTiles - 1; ++i)
            {
                for (int j = 0; j < halfWidthInTiles - 1; ++j)
                {
                    _levelTiles[i, j] = '.';
                }
            }

            Build(halfWidthInTiles, 1, new Vector2Int(halfHeightInTiles - 1, 0), 'B');
            Build(1, halfHeightInTiles, new Vector2Int(0, halfWidthInTiles - 1), 'B');
            // PrintLevel();
            var diggerPos = new Vector2Int(0, 0);

            bool canBuild = true, canMove = true;
            while (canBuild || canMove)
            {
                canBuild = TryBuild(diggerPos);
                canMove = TryDigCorridor(ref diggerPos);
                // PrintLevel();
            }

            var fullLevelTiles = MirrorLevel();
            
            if (ValidateLevel(fullLevelTiles))
            {
                RenderLevel(fullLevelTiles);
                break;
            }
        }
    }

    private bool Free(char[,] tiles, Vector2Int pos)
    {
        return tiles[pos.x, pos.y] != 'W' && tiles[pos.x, pos.y] != 'B';
    }

    private char[,] MirrorLevel()
    {
        // fullLevelTiles is _levelTiles reflected horizontally and vertically - making symmetric level.
        char[,] fullLevelTiles = new char[halfHeightInTiles * 2, halfWidthInTiles * 2];
        // Copy generated tiles
        for (int i = halfHeightInTiles; i < halfHeightInTiles * 2; ++i)
        {
            for (int j = halfWidthInTiles; j < halfWidthInTiles * 2; ++j)
            {
                fullLevelTiles[i, j] = _levelTiles[i - halfHeightInTiles, j - halfWidthInTiles];
            }
        }
        // Reflect horizontally
        for (int i = halfHeightInTiles; i < halfHeightInTiles * 2; ++i)
        {
            for (int j = halfWidthInTiles - 1; j >= 0; --j)
            {
                fullLevelTiles[i, j] = fullLevelTiles[i, halfWidthInTiles + (halfWidthInTiles - j - 1)];
            }
        }
        // Reflect vertically
        for (int i = halfHeightInTiles - 1; i >= 0; --i)
        {
            for (int j = 0; j < halfWidthInTiles * 2; ++j)
            {
                fullLevelTiles[i, j] = fullLevelTiles[halfHeightInTiles + (halfHeightInTiles - i - 1), j];
            }
        }

        return fullLevelTiles;
    }

    private bool ValidateLevel(char[,] fullLevelTiles)
    {
        int height = fullLevelTiles.GetLength(0);
        int width = fullLevelTiles.GetLength(1);
        int countEmptySpaces = 0;
        Vector2Int start = new Vector2Int(0, 0);
        bool[,] visited = new bool[height, width];
        for (int i = 0; i < height; ++i)
        {
            for (int j = 0; j < width; ++j)
            {
                if (Free(fullLevelTiles, new Vector2Int(i, j)))
                {
                    ++countEmptySpaces;
                    start = new Vector2Int(i, j);
                    visited[i, j] = false;
                }
            }
        }

        double percentage = (double) countEmptySpaces / height / width;
        if (percentage < 0.05f || percentage > maxEmptySpaces)
        {
            return false;
        }
        dfs(start, fullLevelTiles, visited);

        int countVisited = 0;
        for (int i = 0; i < height; ++i)
        {
            for (int j = 0; j < width; ++j)
            {
                if (visited[i, j])
                {
                    ++countVisited;
                }
            }
        }
        if (countVisited < countEmptySpaces)
        {
            return false;
        }

        return true;
    }

    private void dfs(Vector2Int currentPos, char[,] tiles, bool[,] visited)
    {
        visited[currentPos.x, currentPos.y] = true;
        foreach (var move in _moves)
        {
            var nextPos = currentPos + move;
            if (nextPos.x < 0 || nextPos.x >= visited.GetLength(0) || nextPos.y < 0 || nextPos.y >= visited.GetLength(1))
            {
                continue;
            }
            if (Free(tiles, nextPos) && !visited[nextPos.x, nextPos.y])
            {
                dfs(nextPos, tiles, visited);
            }
        }
    }

    private void RenderLevel(char[,] fullLevelTiles)
    {
        int height = fullLevelTiles.GetLength(0);
        int width = fullLevelTiles.GetLength(1);
        // Render walls
        for (int i = 1; i < height - 1; ++i)
        {
            for (int j = 1; j < width - 1; ++j)
            {
                if (fullLevelTiles[i, j] != 'W')
                {
                    continue;
                }
                var current = new Vector2Int(i, j);
                var up = current + _moves[(int) MoveDirection.Up];
                var right = current + _moves[(int) MoveDirection.Right];
                var down = current + _moves[(int) MoveDirection.Down];
                var left = current + _moves[(int) MoveDirection.Left];
                
                if (Occupied(fullLevelTiles, new []{up, right, down, left}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.Middle);
                }
                else if (Occupied(fullLevelTiles, new []{right, down, left}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.Top);
                }
                else if (Occupied(fullLevelTiles, new []{up, down, left}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.Right);
                } 
                else if (Occupied(fullLevelTiles, new []{up, right, left}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.Bottom);
                }
                else if (Occupied(fullLevelTiles, new []{up, right, down}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.Left);
                }
                else if (Occupied(fullLevelTiles, new []{right, down}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.TopLeft);
                }
                else if (Occupied(fullLevelTiles, new []{left, down}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.TopRight);
                }
                else if (Occupied(fullLevelTiles, new []{up, left}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.BottomRight);
                }
                else if (Occupied(fullLevelTiles, new []{right, up}))
                {
                    RenderTile(i, j, fullLevelTiles, (int) TileDirection.BottomLeft);
                }
            }
        }

        // Render borders
        for (int i = 1; i < height - 1; ++i)
        {
            RenderTile(i, 0, fullLevelTiles, (int) TileDirection.Left);
            RenderTile(i, width - 1, fullLevelTiles, (int) TileDirection.Right);
        }
        for (int j = 1; j < width - 1; ++j)
        {
            RenderTile(0, j, fullLevelTiles, (int) TileDirection.Bottom);
            RenderTile(height - 1,j, fullLevelTiles, (int) TileDirection.Top);
        }
        RenderTile(0, 0, fullLevelTiles, (int) TileDirection.BottomLeft);
        RenderTile(0, width - 1, fullLevelTiles, (int) TileDirection.BottomRight);
        RenderTile(height - 1, width - 1, fullLevelTiles, (int) TileDirection.TopRight);
        RenderTile(height - 1, 0, fullLevelTiles, (int) TileDirection.TopLeft);
    }

    private void RenderTile(int x, int y, char[,] tileTable, int tile)
    {
        switch (tileTable[x, y])
        {
            case 'W':
                _foreground.SetTile(new Vector3Int(y - halfWidthInTiles, x - halfHeightInTiles, 0), obstacle[tile]);
                break;
            case 'B':
                _foreground.SetTile(new Vector3Int(y - halfWidthInTiles, x - halfHeightInTiles, 0), levelBorder[tile]);
                break;
        }
    }

    private bool Occupied(char[,] tiles, Vector2Int[] cells)
    {
        int count = 0;
        foreach (var cell in cells)
        {
            if (cell.x < 0 || cell.x >= halfHeightInTiles * 2 || cell.y < 0 || cell.y >= halfWidthInTiles * 2)
            {
                count++;
            }
            else if (!Free(tiles, cell))
            {
                count++;
            }
        }
        
        
        return count == cells.Length;
    }

    private void PrintLevel()
    {
        Debug.Log("Level Table");
        for (int i = 0; i < halfHeightInTiles; ++i)
        {
            String tiles = "";
            for (int j = 0; j < halfWidthInTiles; ++j)
            {
                tiles += _levelTiles[i, j];
            }

            Debug.Log(tiles);
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

    private bool TryDigCorridor(ref Vector2Int diggerPos)
    {
        List<int> shuffledIdx = Enumerable.Range(0, _moves.Length).ToList();
        RandomShuffle(shuffledIdx);
        List<int> moveLength = Enumerable.Range(3, 8).ToList();
        RandomShuffle(moveLength);
        
        foreach (var idx in shuffledIdx)
        {
            var dir = _moves[idx];
            foreach (var length in moveLength)
            {
                if (CanDigCorridor(ref diggerPos, dir, length))
                {
                    DigCorridor(ref diggerPos, dir, length);
                    return true; // Return true if managed to dig somewhere
                }
            }
        }

        return false; // Return false if failed to dig anywhere
    }

    private bool CanBuild(int width, int height, Vector2Int startingPos)
    {
        for (int i = startingPos[0]; i < startingPos[0] + height; ++i)
        {
            if (i < 0 || i >= halfHeightInTiles)
            {
                continue;
            }
            for (int j = startingPos[1]; j < startingPos[1] + width; ++j)
            {
                if (j < 0 || j >= halfWidthInTiles)
                {
                    continue;
                }
                if (!Free(_levelTiles, new Vector2Int(i, j)))
                {
                    return false;
                }
            }
        }

        return true;
    }
    
    private bool CanDigCorridor(ref Vector2Int startingPos, Vector2Int dir, int length)
    {
        Vector2Int currentPos = new Vector2Int(startingPos.x, startingPos.y);
        for (int i = 0; i < length; ++i)
        {
            Vector2Int nextPos = currentPos + dir;
            if (nextPos.x < 0 || nextPos.x >= halfHeightInTiles || nextPos.y < 0 || nextPos.y >= halfWidthInTiles)
            {
                // Digging out of bounds
                return false;
            }
            if (_levelTiles[currentPos.x, currentPos.y] != 'W' && _levelTiles[nextPos.x, nextPos.y] == 'W')
            {
                // Intersecting wall
                return false;
            }
            if (_levelTiles[currentPos.x, currentPos.y] != 'C' && _levelTiles[nextPos.x, nextPos.y] == 'C')
            {
                // Intersecting corridor
                return false;
            }
            if (_levelTiles[nextPos.x, nextPos.y] == 'B')
            {
                // Intersecting border
                return false;
            }

            currentPos += dir;
        }

        if (_levelTiles[currentPos.x, currentPos.y] != '.')
        {
            // Move ended not on the empty space
            return false;
        }

        return true;
    }
    
    private void DigCorridor(ref Vector2Int currentPos, Vector2Int dir, int length)
    {
        for (int i = 0; i < length; ++i)
        {
            if (_levelTiles[currentPos.x, currentPos.y] == '.')
            {
                _levelTiles[currentPos.x, currentPos.y] = 'C';
            }

            currentPos += dir;
        }
    }
    
    private void Build(int width, int height, Vector2Int startingPos, char building)
    {
        for (int i = startingPos[0]; i < startingPos[0] + height; ++i)
        {
            if (i < 0 || i >= halfHeightInTiles)
            {
                continue;
            }
            for (int j = startingPos[1]; j < startingPos[1] + width; ++j)
            {
                if (j < 0 || j >= halfWidthInTiles)
                {
                    continue;
                }
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
        _foreground = Instantiate(tilemap, grid.transform).GetComponent<Tilemap>();
        InitTilemap(_foreground, "Foreground");
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
