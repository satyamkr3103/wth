using UnityEngine;

namespace AsciiActionGame.Core
{
    public static class GameConstants
    {
        public const int LAYER_BACKGROUND_TERRAIN = 0;
        public const int LAYER_ENVIRONMENT_DECOR = 10;
        public const int LAYER_GROUND_ITEMS = 50;
        public const int LAYER_PLAYER_TRAIL = 80;
        public const int LAYER_ENEMY_LIMB = 100;
        public const int LAYER_ENEMY_ARMOR = 110;
        public const int LAYER_ENEMY_CORE = 120;
        public const int LAYER_PLAYER = 150;
        public const int LAYER_PROJECTILES = 200;
        public const int LAYER_VFX_PARTICLES = 250;
        public const int LAYER_UI_OVERLAY = 300;

        public const char GLYPH_PLAYER_DEFAULT = '@';
        public const char GLYPH_PLAYER_UP = '^';
        public const char GLYPH_PLAYER_DOWN = 'v';
        public const char GLYPH_PLAYER_LEFT = '<';
        public const char GLYPH_PLAYER_RIGHT = '>';

        public const char GLYPH_WALL_SOLID = '#';
        public const char GLYPH_WALL_DENSE = '%';
        public const char GLYPH_WALL_CORRUPT = '&';
        public const char GLYPH_GROUND_DOT = '.';
        public const char GLYPH_GROUND_COMMA = ',';
        public const char GLYPH_GROUND_COLON = ':';
        public const char GLYPH_GROUND_SEMICOLON = ';';
        public const char GLYPH_DATA_ZERO = '0';
        public const char GLYPH_DATA_ONE = '1';

        public const char GLYPH_ENEMY_CORE = 'X';
        public const char GLYPH_ENEMY_ARMOR = '#';
        public const char GLYPH_ENEMY_ARM_LEFT = '{';
        public const char GLYPH_ENEMY_ARM_RIGHT = '}';
        public const char GLYPH_ENEMY_LEG = '|';
        public const char GLYPH_FRAGMENT = '*';

        public const char GLYPH_BEAM_HORIZONTAL = '=';
        public const char GLYPH_BEAM_VERTICAL = '|';
        public const char GLYPH_BEAM_DIAGONAL_FWD = '/';
        public const char GLYPH_BEAM_DIAGONAL_BACK = '\\';
        public const char GLYPH_BEAM_CORE = '+';

        public const char GLYPH_WATER_WAVE = '~';
        public const char GLYPH_WATER_RIPPLE = '^';
        public const char GLYPH_WATER_SURFACE = '-';

        public const byte TILE_TYPE_VOID = 0;
        public const byte TILE_TYPE_GROUND = 1;
        public const byte TILE_TYPE_WALL = 2;
        public const byte TILE_TYPE_OBSTACLE = 3;
        public const byte TILE_TYPE_DATASTREAM = 4;
        public const byte TILE_TYPE_VEGETATION = 5;
        public const byte TILE_TYPE_CANOPY = 6;
        public const byte TILE_TYPE_PATH = 7;
        public const byte TILE_TYPE_WATER = 8;

        public const string LAYER_NAME_DEFAULT = "Default";
        public const string LAYER_NAME_PLAYER = "Player";
        public const string LAYER_NAME_ENEMY = "Enemy";
        public const string LAYER_NAME_PROJECTILE = "Projectile";
        public const string LAYER_NAME_OBSTACLE = "Obstacle";
    }
}
