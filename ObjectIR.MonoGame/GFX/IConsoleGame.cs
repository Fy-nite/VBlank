using Adamantite.GFX;

namespace Adamantite.GFX
{
    public interface IConsoleGame
    {
        void Init(Canvas surface);
        void Update(double deltaTime);
        void Draw(Canvas surface);
    }
}
