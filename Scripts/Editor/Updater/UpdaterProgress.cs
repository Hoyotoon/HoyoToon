#if UNITY_EDITOR
namespace HoyoToon.Editor.Updater
{
    internal interface IProgressSink
    {
        void Report(string title, string info, float progress01);
    }
}
#endif
