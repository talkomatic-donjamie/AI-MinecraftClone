using System;
using System.Numerics;
using Raylib_cs;

class Program
{
    const float MAX_STEP_HEIGHT = 0f;
    const int screenWidth = 800;
    const int screenHeight = 600;
    static Camera3D camera = new Camera3D();
    static float yaw = -90.0f;
    static float pitch = 0.0f;
    static Texture2D grassTexture;
    static Texture2D dirtTexture;

    // Variables for jumping and gravity
    const float GRAVITY = -20.0f;
    const float JUMP_FORCE = 9.0f;
    const float MOVE_SPEED = 5.0f;
    static float verticalSpeed = 0.0f;
    static bool isJumping = false;
    static float groundCheckDistance = 0.1f;

    static Vector3 playerVelocity = Vector3.Zero;

    // Player variables
    static Vector3 playerPosition;
    const float PUSH_OUT_STRENGTH = 0.1f;   // New player height
    static bool isOnGround = false;
    static float playerHeight = 2.5f;
    static float playerWidth = 0.8f;
    const float GROUND_OFFSET = 0.05f;
    const float GROUND_CHECK_DISTANCE = 0.1f;
    const float JUMP_GRACE_DISTANCE = 0.2f;

    static Rendering renderer;

    static void Main(string[] args)
    {
        Raylib.InitWindow(screenWidth, screenHeight, "3D Minecraft Clone");
        Raylib.SetTargetFPS(60);

        // Initialize camera and player position
        playerPosition = new Vector3(0.0f, 20.0f, 0.0f);
        camera.Position = new Vector3(playerPosition.X, playerPosition.Y + playerHeight / 2, playerPosition.Z);
        camera.Target = new Vector3(playerPosition.X, playerPosition.Y + playerHeight / 2, playerPosition.Z - 1.0f);
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        camera.FovY = 45.0f;
        camera.Projection = CameraProjection.Perspective;

        LoadTextures();
        renderer = new Rendering(grassTexture, dirtTexture);

        Raylib.DisableCursor();

        while (!Raylib.WindowShouldClose())
        {
            UpdatePlayerAndCamera();
            ApplyGravity();

            // Update the selected cube position
            renderer.UpdateSelectedCube(camera);

            // Handle input for cube placement and breaking
            renderer.HandleInput();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.SkyBlue);

            Raylib.BeginMode3D(camera);
            renderer.DrawWorld(camera);
            Raylib.EndMode3D();

            // Draw crosshair
            Raylib.DrawLine(screenWidth / 2 - 10, screenHeight / 2, screenWidth / 2 + 10, screenHeight / 2, Color.White);
            Raylib.DrawLine(screenWidth / 2, screenHeight / 2 - 10, screenWidth / 2, screenHeight / 2 + 10, Color.White);

            Raylib.DrawText("Use WASD to move, Mouse to look around, Space to jump", 10, 10, 20, Color.DarkGray);
            Raylib.DrawText("Left click to place/break, Right click to switch mode", 10, 40, 20, Color.DarkGray);
            Raylib.EndDrawing();
        }

        Raylib.UnloadTexture(grassTexture);
        Raylib.UnloadTexture(dirtTexture);
        Raylib.CloseWindow();
    }

    static void LoadTextures()
    {
        try
        {
            grassTexture = Raylib.LoadTexture("Assets/grass.png");
            dirtTexture = Raylib.LoadTexture("Assets/dirt.png");
            Console.WriteLine("Textures loaded successfully.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error loading textures: {e.Message}");
        }
    }

    static void UpdatePlayerAndCamera()
    {
        float deltaTime = Raylib.GetFrameTime();
        Vector2 mouseDelta = Raylib.GetMouseDelta();

        // Update camera rotation
        yaw += mouseDelta.X * 0.1f;
        pitch -= mouseDelta.Y * 0.1f;
        pitch = Math.Clamp(pitch, -89.0f, 89.0f);

        // Reset mouse position to center of screen
        Raylib.SetMousePosition(screenWidth / 2, screenHeight / 2);

        Vector3 direction = new Vector3(
            (float)(Math.Cos(yaw * Math.PI / 180.0) * Math.Cos(pitch * Math.PI / 180.0)),
            (float)(Math.Sin(pitch * Math.PI / 180.0)),
            (float)(Math.Sin(yaw * Math.PI / 180.0) * Math.Cos(pitch * Math.PI / 180.0))
        );
        direction = Vector3.Normalize(direction);

        Vector3 right = Vector3.Normalize(Vector3.Cross(direction, camera.Up));

        Vector3 movement = Vector3.Zero;

        if (Raylib.IsKeyDown(KeyboardKey.W)) movement += new Vector3(direction.X, 0, direction.Z);
        if (Raylib.IsKeyDown(KeyboardKey.S)) movement -= new Vector3(direction.X, 0, direction.Z);
        if (Raylib.IsKeyDown(KeyboardKey.A)) movement -= right;
        if (Raylib.IsKeyDown(KeyboardKey.D)) movement += right;

        if (movement != Vector3.Zero)
        {
            movement = Vector3.Normalize(movement) * MOVE_SPEED * deltaTime;
        }

        // Apply gravity
        verticalSpeed += GRAVITY * deltaTime;

        // Check if player can jump
        bool canJump = CheckCanJump();

        // Jumping logic
        if (canJump && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            verticalSpeed = JUMP_FORCE;
            isOnGround = false;
        }

        // Combine horizontal movement and vertical speed
        Vector3 finalMovement = new Vector3(movement.X, verticalSpeed * deltaTime, movement.Z);
        MovePlayer(finalMovement);

        // Update camera position based on player position
        camera.Position = new Vector3(playerPosition.X, playerPosition.Y + playerHeight / 2, playerPosition.Z);
        camera.Target = Vector3.Add(camera.Position, direction);

        // Perform final ground correction
        CorrectGroundClipping();
    }

    static void MovePlayer(Vector3 movement)
    {
        Vector3 originalPosition = playerPosition;
        Vector3 newPosition = originalPosition + movement;

        // Handle horizontal movement
        newPosition = HandleHorizontalCollision(originalPosition, newPosition);

        // Handle vertical movement
        newPosition = HandleVerticalCollision(originalPosition, newPosition);

        // Apply the new position
        playerPosition = newPosition;

        // Update ground state
        isOnGround = CheckOnGround();

        // Prevent falling through the world
        if (playerPosition.Y < -50)
        {
            playerPosition.Y = 20;
            verticalSpeed = 0;
        }
    }

    static bool CheckCanJump()
    {
        Vector3 feetPosition = playerPosition - new Vector3(0, playerHeight / 2, 0);
        foreach (Vector3 cubePosition in renderer.GetCubePositions())
        {
            if (Math.Abs(feetPosition.X - cubePosition.X) < (1 + playerWidth / 2) &&
                Math.Abs(feetPosition.Z - cubePosition.Z) < (1 + playerWidth / 2))
            {
                float distanceToGround = feetPosition.Y - (cubePosition.Y + 2);
                if (distanceToGround <= JUMP_GRACE_DISTANCE)
                {
                    return true;
                }
            }
        }
        return false;
    }

    static void CorrectGroundClipping()
    {
        Vector3 feetPosition = playerPosition - new Vector3(0, playerHeight / 2, 0);
        float highestGroundPoint = float.MinValue;

        foreach (Vector3 cubePosition in renderer.GetCubePositions())
        {
            if (Math.Abs(feetPosition.X - cubePosition.X) < (1 + playerWidth / 2) &&
                Math.Abs(feetPosition.Z - cubePosition.Z) < (1 + playerWidth / 2))
            {
                float topOfCube = cubePosition.Y + 2; // Assuming cube height is 2
                if (topOfCube > highestGroundPoint && feetPosition.Y <= topOfCube + GROUND_CHECK_DISTANCE)
                {
                    highestGroundPoint = topOfCube;
                }
            }
        }

        if (highestGroundPoint > float.MinValue)
        {
            float desiredY = highestGroundPoint + playerHeight / 2 + GROUND_OFFSET;
            if (playerPosition.Y < desiredY)
            {
                playerPosition.Y = desiredY;
                verticalSpeed = Math.Max(verticalSpeed, 0); // Prevent negative vertical speed when on ground
                isOnGround = true;
            }
        }
    }

    static void EnsureAboveGround()
    {
        Vector3 feetPosition = playerPosition - new Vector3(0, playerHeight / 2, 0);
        foreach (Vector3 cubePosition in renderer.GetCubePositions())
        {
            if (CheckCollisionPlayerCube(playerPosition, cubePosition, new Vector3(2, 2, 2)))
            {
                float desiredY = cubePosition.Y + 2 + playerHeight / 2 + GROUND_OFFSET;
                if (playerPosition.Y < desiredY)
                {
                    playerPosition.Y = desiredY;
                    verticalSpeed = 0;
                    isOnGround = true;
                }
                break;
            }
        }
    }

    static Vector3 HandleCollision(Vector3 originalPosition, Vector3 newPosition)
    {
        Vector3 adjustedPosition = newPosition;

        // Check horizontal movement first (X and Z axes)
        Vector3 horizontalMovement = new Vector3(newPosition.X - originalPosition.X, 0, newPosition.Z - originalPosition.Z);
        if (horizontalMovement != Vector3.Zero)
        {
            adjustedPosition = HandleHorizontalCollision(originalPosition, adjustedPosition);
        }

        // Then check vertical movement (Y axis)
        adjustedPosition = HandleVerticalCollision(originalPosition, adjustedPosition);

        return adjustedPosition;
    }

    static Vector3 HandleHorizontalCollision(Vector3 originalPosition, Vector3 newPosition)
    {
        Vector3 horizontalMovement = new Vector3(newPosition.X - originalPosition.X, 0, newPosition.Z - originalPosition.Z);
        if (horizontalMovement == Vector3.Zero) return newPosition;

        Vector3 adjustedPosition = newPosition;

        foreach (Vector3 cubePosition in renderer.GetCubePositions())
        {
            if (CheckCollisionPlayerCube(new Vector3(adjustedPosition.X, originalPosition.Y, adjustedPosition.Z), cubePosition, new Vector3(2, 2, 2)))
            {
                // Try sliding along X axis
                Vector3 slideX = new Vector3(originalPosition.X, adjustedPosition.Y, adjustedPosition.Z);
                if (!CheckCollisionPlayerCube(slideX, cubePosition, new Vector3(2, 2, 2)))
                {
                    adjustedPosition = slideX;
                    continue;
                }

                // Try sliding along Z axis
                Vector3 slideZ = new Vector3(adjustedPosition.X, adjustedPosition.Y, originalPosition.Z);
                if (!CheckCollisionPlayerCube(slideZ, cubePosition, new Vector3(2, 2, 2)))
                {
                    adjustedPosition = slideZ;
                    continue;
                }

                // If both fail, revert to original position
                adjustedPosition = originalPosition;
                break;
            }
        }

        return adjustedPosition;
    }

    static Vector3 HandleVerticalCollision(Vector3 originalPosition, Vector3 newPosition)
    {
        Vector3 adjustedPosition = newPosition;
        Vector3 feetPosition = adjustedPosition - new Vector3(0, playerHeight / 2, 0);

        foreach (Vector3 cubePosition in renderer.GetCubePositions())
        {
            if (CheckCollisionPlayerCube(adjustedPosition, cubePosition, new Vector3(2, 2, 2)))
            {
                if (newPosition.Y > originalPosition.Y)
                {
                    // Moving upwards, set position to bottom of obstacle
                    adjustedPosition.Y = cubePosition.Y - playerHeight / 2 - GROUND_OFFSET;
                    verticalSpeed = 0;
                }
                else
                {
                    // Moving downwards, set position to top of obstacle
                    adjustedPosition.Y = cubePosition.Y + 2 + playerHeight / 2 + GROUND_OFFSET;
                    isOnGround = true;
                    verticalSpeed = 0;
                }
                break;
            }
        }

        return adjustedPosition;
    }

    static bool CheckOnGround()
    {
        Vector3 feetPosition = playerPosition - new Vector3(0, playerHeight / 2, 0);
        foreach (Vector3 cubePosition in renderer.GetCubePositions())
        {
            if (Math.Abs(feetPosition.X - cubePosition.X) < (1 + playerWidth / 2) &&
                Math.Abs(feetPosition.Z - cubePosition.Z) < (1 + playerWidth / 2))
            {
                float distanceToGround = feetPosition.Y - (cubePosition.Y + 2);
                if (distanceToGround <= GROUND_CHECK_DISTANCE)
                {
                    return true;
                }
            }
        }
        return false;
    }

    static bool TryStepUp(Vector3 originalPosition, Vector3 newPosition, Vector3 obstaclePosition)
    {
        float stepHeight = obstaclePosition.Y + 2 - originalPosition.Y; // Cube height is 2
        if (stepHeight <= MAX_STEP_HEIGHT)
        {
            Vector3 stepUpPosition = new Vector3(newPosition.X, obstaclePosition.Y + 2 + 0.01f, newPosition.Z);
            if (!CheckCollisionWithAnyCube(stepUpPosition))
            {
                newPosition.Y = stepUpPosition.Y;
                return true;
            }
        }
        return false;
    }

    static Vector3 SlideAlongObstacle(Vector3 originalPosition, Vector3 newPosition, Vector3 obstaclePosition)
    {
        // Try sliding along X axis
        Vector3 slideX = new Vector3(originalPosition.X, newPosition.Y, newPosition.Z);
        if (!CheckCollisionPlayerCube(slideX, obstaclePosition, new Vector3(2, 2, 2)))
        {
            return slideX;
        }

        // Try sliding along Z axis
        Vector3 slideZ = new Vector3(newPosition.X, newPosition.Y, originalPosition.Z);
        if (!CheckCollisionPlayerCube(slideZ, obstaclePosition, new Vector3(2, 2, 2)))
        {
            return slideZ;
        }

        // If both fail, return original position
        return originalPosition;
    }

    static bool CheckCollisionWithAnyCube(Vector3 position)
    {
        foreach (Vector3 cubePosition in renderer.GetCubePositions())
        {
            if (CheckCollisionPlayerCube(position, cubePosition, new Vector3(2, 2, 2)))
            {
                return true;
            }
        }
        return false;
    }

    static void ApplyGravity()
    {
        // This method is now handled within MovePlayer, so we can leave it empty or remove it
    }

    static bool CheckCollisionPlayerCube(Vector3 playerPos, Vector3 cubePos, Vector3 cubeSize)
    {
        return (Math.Abs(playerPos.X - cubePos.X) < (cubeSize.X / 2 + playerWidth / 2)) &&
               (Math.Abs(playerPos.Y - cubePos.Y) < (cubeSize.Y / 2 + playerHeight / 2)) &&
               (Math.Abs(playerPos.Z - cubePos.Z) < (cubeSize.Z / 2 + playerWidth / 2));
    }

}