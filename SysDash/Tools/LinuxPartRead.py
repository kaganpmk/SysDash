"""Harfsiz bolum okuyucu (Linux ext2/3/4 doluluk).

Windows bu bolumlere surucu harfi vermedigi icin normal API'lerle erisilemez;
dosya sistemi surucusu yok. Tek yol ham disk okumak ve bu YONETICI hakki ister.
SysDash bunu yonetici roluyle ayri surecte calistirir, sonucu JSON olarak alir.

Kullanim:
    py -3 LinuxPartRead.py <offset> <diskNo> <sizeBayt> [ciktiDosyasi]
"""
import ctypes
import json
import struct
import sys

EXT4_MAGIC = 0xEF53


def read_ext4_superblock(offset, disk_no, size_bytes):
    k32 = ctypes.windll.kernel32
    k32.CreateFileW.restype = ctypes.c_void_p

    handle = k32.CreateFileW(
        "\\\\.\\PhysicalDrive" + str(int(disk_no)),
        0x80000000,  # GENERIC_READ
        3,           # FILE_SHARE_READ | FILE_SHARE_WRITE
        None,
        3,           # OPEN_EXISTING
        0,
        None,
    )
    if not handle or handle == 0xFFFFFFFFFFFFFFFF:
        return {"ok": False, "error": "disk okunamadi (yonetici hakki gerekli)"}

    try:
        start = ctypes.c_longlong(offset + 1024)
        k32.SetFilePointerEx(ctypes.c_void_p(handle), start, None, 0)
        buf = ctypes.create_string_buffer(1024)
        got = ctypes.c_ulong(0)
        if not k32.ReadFile(ctypes.c_void_p(handle), buf, 1024, ctypes.byref(got), None):
            return {"ok": False, "error": "superblock okunamadi"}

        sb = buf.raw
        magic = struct.unpack_from("<H", sb, 0x38)[0]
        if magic != EXT4_MAGIC:
            return {"ok": False, "error": "ext4 degil (magic=0x%04X)" % magic}

        blocks = struct.unpack_from("<I", sb, 0x04)[0] | (struct.unpack_from("<I", sb, 0x150)[0] << 32)
        free = struct.unpack_from("<I", sb, 0x0C)[0] | (struct.unpack_from("<I", sb, 0x158)[0] << 32)
        block_size = 1024 << struct.unpack_from("<I", sb, 0x18)[0]

        return {
            "ok": True,
            "total_gb": round(blocks * block_size / 1024 ** 3, 2),
            "free_gb": round(free * block_size / 1024 ** 3, 2),
            "size_gb": round(size_bytes / 1024 ** 3, 1),
            "label": sb[0x78:0x88].rstrip(b"\0").decode("utf-8", "replace"),
        }
    finally:
        k32.CloseHandle(ctypes.c_void_p(handle))


def main():
    if len(sys.argv) < 4:
        print(json.dumps({"ok": False, "error": "kullanim: offset diskNo sizeBayt [cikti]"}))
        return 1

    result = read_ext4_superblock(int(sys.argv[1]), sys.argv[2], int(sys.argv[3]))

    if len(sys.argv) > 4:
        with open(sys.argv[4], "w", encoding="utf-8") as f:
            json.dump(result, f)

    print(json.dumps(result))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main())
