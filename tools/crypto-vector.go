package main

import (
	"crypto/aes"
	"crypto/cipher"
	"crypto/sha256"
	"encoding/binary"
	"encoding/hex"
	"fmt"
)

func main() {
	key := make([]byte, 32)
	for i := range key { key[i] = byte(i) }
	plain := []byte{0, 1, 2, 3, 4, 5, 254, 255}
	var counter [8]byte
	binary.LittleEndian.PutUint64(counter[:], 0)
	h := sha256.New()
	h.Write(counter[:]); h.Write(plain); h.Write(key)
	data := append(append([]byte(nil), plain...), h.Sum(nil)[:8]...)
	block, _ := aes.NewCipher(key)
	iv := append(append([]byte(nil), key[:12]...), 0, 0, 0, 2)
	cipher.NewCTR(block, iv).XORKeyStream(data, data)
	fmt.Println(hex.EncodeToString(data))
}
