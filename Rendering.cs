using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public class Rendering
{
    private List<Vector3> cubePositions;
    private Texture2D grassTexture;
    private Texture2D dirtTexture;
    private int worldSize = 20;
    private float cubeSize = 2f;

    private bool isPlaceMode = true;
    private Vector3? selectedCubePosition;
    private Vector3? placePosition;
    private float selectionAlpha = 0.5f;
    private float selectionAlphaSpeed = 2f;
    private float maxReachDistance = 4f * 2f;

    public Rendering(Texture2D grassTexture, Texture2D dirtTexture)
    {
        this.grassTexture = grassTexture;
        this.dirtTexture = dirtTexture;
        cubePositions = new List<Vector3>();
        GenerateWorld();
    }

    private void GenerateWorld()
    {
        Random random = new Random();
        float[,] heightMap = new float[worldSize, worldSize];

        // Generate a simple heightmap
        for (int x = 0; x < worldSize; x++)
        {
            for (int z = 0; z < worldSize; z++)
            {
                heightMap[x, z] = (float)(Math.Sin(x * 0.2) + Math.Cos(z * 0.2)) * 2 + random.Next(0, 3);
            }
        }

        // Create cubes based on the heightmap
        for (int x = 0; x < worldSize; x++)
        {
            for (int z = 0; z < worldSize; z++)
            {
                int height = (int)Math.Max(1, heightMap[x, z]);
                for (int y = 0; y < height; y++)
                {
                    Vector3 position = new Vector3(x * cubeSize - worldSize, y * cubeSize, z * cubeSize - worldSize);
                    cubePositions.Add(position);
                }
            }
        }
    }

    public void DrawWorld(Camera3D camera)
    {
        foreach (Vector3 position in cubePositions)
        {
            Texture2D texture = position.Y > 0 ? dirtTexture : grassTexture;
            DrawCubeTextured(texture, position, cubeSize, cubeSize, cubeSize, camera);
        }

        // Draw the selected cube
        DrawSelectedCube(camera);
    }

    public List<Vector3> GetCubePositions()
    {
        return cubePositions;
    }

    public void DrawCubeTextured(Texture2D texture, Vector3 position, float width, float height, float length, Camera3D camera)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z;

        // Define the vertices of the cube (counter-clockwise)
        Vector3[] vertices = new Vector3[]
        {
            // Front face
            new Vector3(x - width / 2, y - height / 2, z + length / 2),
            new Vector3(x + width / 2, y - height / 2, z + length / 2),
            new Vector3(x + width / 2, y + height / 2, z + length / 2),
            new Vector3(x - width / 2, y + height / 2, z + length / 2),

            // Back face
            new Vector3(x - width / 2, y - height / 2, z - length / 2),
            new Vector3(x - width / 2, y + height / 2, z - length / 2),
            new Vector3(x + width / 2, y + height / 2, z - length / 2),
            new Vector3(x + width / 2, y - height / 2, z - length / 2),

            // Left face
            new Vector3(x - width / 2, y - height / 2, z + length / 2),
            new Vector3(x - width / 2, y + height / 2, z + length / 2),
            new Vector3(x - width / 2, y + height / 2, z - length / 2),
            new Vector3(x - width / 2, y - height / 2, z - length / 2),

            // Right face
            new Vector3(x + width / 2, y - height / 2, z + length / 2),
            new Vector3(x + width / 2, y - height / 2, z - length / 2),
            new Vector3(x + width / 2, y + height / 2, z - length / 2),
            new Vector3(x + width / 2, y + height / 2, z + length / 2),

            // Top face
            new Vector3(x - width / 2, y + height / 2, z + length / 2),
            new Vector3(x + width / 2, y + height / 2, z + length / 2),
            new Vector3(x + width / 2, y + height / 2, z - length / 2),
            new Vector3(x - width / 2, y + height / 2, z - length / 2),

            // Bottom face
            new Vector3(x - width / 2, y - height / 2, z + length / 2),
            new Vector3(x - width / 2, y - height / 2, z - length / 2),
            new Vector3(x + width / 2, y - height / 2, z - length / 2),
            new Vector3(x + width / 2, y - height / 2, z + length / 2),
        };

        // Set the texture and draw mode
        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);

        // Set color
        Rlgl.Color4ub(255, 255, 255, 255);

        // Draw all faces
        for (int i = 0; i < vertices.Length; i += 4)
        {
            Rlgl.TexCoord2f(0.0f, 0.0f); Rlgl.Vertex3f(vertices[i].X, vertices[i].Y, vertices[i].Z);
            Rlgl.TexCoord2f(1.0f, 0.0f); Rlgl.Vertex3f(vertices[i + 1].X, vertices[i + 1].Y, vertices[i + 1].Z);
            Rlgl.TexCoord2f(1.0f, 1.0f); Rlgl.Vertex3f(vertices[i + 2].X, vertices[i + 2].Y, vertices[i + 2].Z);
            Rlgl.TexCoord2f(0.0f, 1.0f); Rlgl.Vertex3f(vertices[i + 3].X, vertices[i + 3].Y, vertices[i + 3].Z);
        }

        Rlgl.End();
    }

    public void UpdateSelectedCube(Camera3D camera)
    {
        Ray ray = Raylib.GetMouseRay(new Vector2(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2), camera);
        float closestDistance = maxReachDistance;
        Vector3 hitPosition = Vector3.Zero;
        bool hitFound = false;

        selectedCubePosition = null;
        placePosition = null;

        // First, check for cube hits
        foreach (Vector3 cubePos in cubePositions)
        {
            if (CheckCollisionRayBox(ray,
                new BoundingBox(
                    new Vector3(cubePos.X - cubeSize / 2, cubePos.Y - cubeSize / 2, cubePos.Z - cubeSize / 2),
                    new Vector3(cubePos.X + cubeSize / 2, cubePos.Y + cubeSize / 2, cubePos.Z + cubeSize / 2)
                ), out float distanceToHit))
            {
                if (distanceToHit < closestDistance)
                {
                    closestDistance = distanceToHit;
                    hitPosition = Vector3.Add(ray.Position, Vector3.Multiply(ray.Direction, distanceToHit));
                    hitFound = true;
                    selectedCubePosition = cubePos;
                }
            }
        }

        if (hitFound)
        {
            Vector3 normal = CalculateNormal(hitPosition, closestDistance, ray);

            if (isPlaceMode)
            {
                placePosition = selectedCubePosition.Value + normal * cubeSize;
            }
        }
        else if (isPlaceMode)
        {
            // If no cube was hit, check for valid placement positions along the ray
            for (float distance = 0; distance <= maxReachDistance; distance += cubeSize / 2)
            {
                Vector3 checkPosition = Vector3.Add(ray.Position, Vector3.Multiply(ray.Direction, distance));
                Vector3 gridPosition = new Vector3(
                    (float)Math.Round(checkPosition.X / cubeSize) * cubeSize,
                    (float)Math.Round(checkPosition.Y / cubeSize) * cubeSize,
                    (float)Math.Round(checkPosition.Z / cubeSize) * cubeSize
                );

                if (IsValidPlacePosition(gridPosition))
                {
                    placePosition = gridPosition;
                    break;
                }
            }
        }

        // Update selection alpha for flashing effect
        selectionAlpha += selectionAlphaSpeed * Raylib.GetFrameTime();
        if (selectionAlpha > 1f || selectionAlpha < 0.2f)
        {
            selectionAlphaSpeed = -selectionAlphaSpeed;
            selectionAlpha = Math.Clamp(selectionAlpha, 0.2f, 1f);
        }
    }

    private bool IsValidPlacePosition(Vector3 position)
    {
        // Check if the position is not inside a cube
        if (cubePositions.Contains(position))
        {
            return false;
        }

        // Check if the position has at least one adjacent cube
        Vector3[] adjacentPositions = new Vector3[]
        {
        new Vector3(position.X + cubeSize, position.Y, position.Z),
        new Vector3(position.X - cubeSize, position.Y, position.Z),
        new Vector3(position.X, position.Y + cubeSize, position.Z),
        new Vector3(position.X, position.Y - cubeSize, position.Z),
        new Vector3(position.X, position.Y, position.Z + cubeSize),
        new Vector3(position.X, position.Y, position.Z - cubeSize)
        };

        foreach (Vector3 adjacentPos in adjacentPositions)
        {
            if (cubePositions.Contains(adjacentPos))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 CalculateNormal(Vector3 hitPosition, float distance, Ray ray)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Subtract(hitPosition, ray.Position));
        float epsilon = 0.001f;

        if (Math.Abs(normal.X) > Math.Abs(normal.Y) && Math.Abs(normal.X) > Math.Abs(normal.Z))
        {
            normal = new Vector3(Math.Sign(normal.X), 0, 0);
        }
        else if (Math.Abs(normal.Y) > Math.Abs(normal.Z))
        {
            normal = new Vector3(0, Math.Sign(normal.Y), 0);
        }
        else
        {
            normal = new Vector3(0, 0, Math.Sign(normal.Z));
        }

        return normal;
    }

    private void DrawSelectedCube(Camera3D camera)
    {
        if (isPlaceMode && placePosition.HasValue)
        {
            Color selectionColor = Color.Green;
            selectionColor.A = (byte)(selectionAlpha * 255);

            Raylib.DrawCubeWires(placePosition.Value, cubeSize, cubeSize, cubeSize, selectionColor);
            Raylib.DrawCube(placePosition.Value, cubeSize, cubeSize, cubeSize, new Color((byte)selectionColor.R, (byte)selectionColor.G, (byte)selectionColor.B, (byte)50));
        }
        else if (!isPlaceMode && selectedCubePosition.HasValue)
        {
            Color selectionColor = Color.Red;
            selectionColor.A = (byte)(selectionAlpha * 255);

            Raylib.DrawCubeWires(selectedCubePosition.Value, cubeSize, cubeSize, cubeSize, selectionColor);
            Raylib.DrawCube(selectedCubePosition.Value, cubeSize, cubeSize, cubeSize, new Color((byte)selectionColor.R, (byte)selectionColor.G, (byte)selectionColor.B, (byte)50));
        }
    }

    // Custom implementation of CheckCollisionRayBox
    private bool CheckCollisionRayBox(Ray ray, BoundingBox box, out float distance)
    {
        Vector3 invDir = new Vector3(1.0f / ray.Direction.X, 1.0f / ray.Direction.Y, 1.0f / ray.Direction.Z);
        Vector3 tMin = Vector3.Multiply(Vector3.Subtract(box.Min, ray.Position), invDir);
        Vector3 tMax = Vector3.Multiply(Vector3.Subtract(box.Max, ray.Position), invDir);

        Vector3 t1 = Vector3.Min(tMin, tMax);
        Vector3 t2 = Vector3.Max(tMin, tMax);

        float tNear = MathF.Max(MathF.Max(t1.X, t1.Y), t1.Z);
        float tFar = MathF.Min(MathF.Min(t2.X, t2.Y), t2.Z);

        distance = tNear;
        return tFar >= tNear && tNear >= 0;
    }

    public void HandleInput()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (isPlaceMode)
            {
                PlaceCube();
            }
            else
            {
                BreakCube();
            }
        }
        else if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            isPlaceMode = !isPlaceMode;
        }
    }

    private void PlaceCube()
    {
        if (placePosition.HasValue && !cubePositions.Contains(placePosition.Value))
        {
            cubePositions.Add(placePosition.Value);
        }
    }

    private void BreakCube()
    {
        if (selectedCubePosition.HasValue)
        {
            cubePositions.Remove(selectedCubePosition.Value);
        }
    }
}