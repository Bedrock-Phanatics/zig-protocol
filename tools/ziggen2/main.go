package main

import (
	"encoding/json"
	"fmt"
	"os"
	"regexp"
	"sort"
	"strings"
	"unicode"
)
type manifest struct { Packets []packet `json:"packets"` }
type packet struct { Name string `json:"name"`; ID uint16 `json:"id"` }
func snake(s string) string { var b strings.Builder; for i, r := range s { if unicode.IsUpper(r) && i > 0 { b.WriteByte('_') }; b.WriteRune(unicode.ToLower(r)) }; return strings.ReplaceAll(b.String(), "u_u_i_d", "uuid") }
func must(err error) { if err != nil { panic(err) } }
func main() {
	data, err := os.ReadFile("protocol/schema/bedrock-2192.json"); must(err)
	var m manifest; must(json.Unmarshal(data, &m)); sort.Slice(m.Packets, func(i, j int) bool { return m.Packets[i].ID < m.Packets[j].ID })
	var out strings.Builder; out.WriteString("// Generated packet model catalog.\n")
	valid := regexp.MustCompile(`^[A-Za-z][A-Za-z0-9]*$`)
	for _, p := range m.Packets { if p.ID <= 1023 && valid.MatchString(p.Name) { n := snake(p.Name); out.WriteString(fmt.Sprintf("pub const %s = @import(\"generated/%s.zig\").Packet;\n", n, n)) } }
	must(os.WriteFile("src/packets/generated.zig", []byte(out.String()), 0644))
}
