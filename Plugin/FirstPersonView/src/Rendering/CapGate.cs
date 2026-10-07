namespace FirstPersonView;

internal sealed class CapGate
{
    public bool On { get; private set; }

    public bool Update(float height, bool standing, bool crouching)
    {
        if (crouching)
        {
            On = false;
            return false;
        }

        float on = standing ? Constants.Cap.StandOnHeight : Constants.Cap.OnHeight;
        float off = standing ? Constants.Cap.StandOffHeight : Constants.Cap.OffHeight;
        On = height >= (On ? off : on);
        return On;
    }

    public void Reset() => On = false;
}
