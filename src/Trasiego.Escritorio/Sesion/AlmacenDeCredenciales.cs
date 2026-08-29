using System.Runtime.InteropServices;
using System.Text;

namespace Trasiego.Escritorio.Sesion;

/// <summary>
/// Guarda un secreto en el administrador de credenciales de Windows.
/// </summary>
/// <remarks>
/// No es un fichero en una carpeta. Windows lo cifra con la cuenta de quien ha entrado, asi
/// que copiarlo a otro sitio no sirve de nada, y ademas sale en el panel de credenciales del
/// sistema: quien quiera olvidar la sesion de esta maquina puede hacerlo sin abrir Trasiego.
/// </remarks>
public static class AlmacenDeCredenciales
{
    private const uint Generica = 1;
    private const uint SoloEstaMaquina = 2;

    public static void Guardar(string nombre, string secreto)
    {
        var bytes = Encoding.UTF8.GetBytes(secreto);

        var titulo = Marshal.StringToCoTaskMemUni(nombre);
        var contenido = Marshal.AllocCoTaskMem(bytes.Length);

        try
        {
            Marshal.Copy(bytes, 0, contenido, bytes.Length);

            var credencial = new CREDENTIAL
            {
                Type = Generica,
                TargetName = titulo,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = contenido,
                Persist = SoloEstaMaquina,
            };

            if (!CredWrite(ref credencial, 0))
                throw new InvalidOperationException(
                    $"Windows no ha dejado guardar la credencial: {Marshal.GetLastWin32Error()}.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(titulo);
            Marshal.FreeCoTaskMem(contenido);
        }
    }

    /// <summary>El secreto guardado, o nada si no hay ninguno con ese nombre.</summary>
    public static string? Leer(string nombre)
    {
        if (!CredRead(nombre, Generica, 0, out var puntero)) return null;

        try
        {
            var credencial = Marshal.PtrToStructure<CREDENTIAL>(puntero);
            if (credencial.CredentialBlobSize == 0) return null;

            var bytes = new byte[credencial.CredentialBlobSize];
            Marshal.Copy(credencial.CredentialBlob, bytes, 0, bytes.Length);

            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CredFree(puntero);
        }
    }

    public static void Olvidar(string nombre) => CredDelete(nombre, Generica, 0);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credencial, uint banderas);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string nombre, uint tipo, uint banderas, out IntPtr credencial);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string nombre, uint tipo, uint banderas);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credencial);

    // El orden de los campos es el que espera Windows y no se puede tocar.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
