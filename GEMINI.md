# DolphinX / DolphinXVDO Project Rules

## 1. MANDATORY AUTOMATIC VERSION BUMP (สำคัญสูงสุด)
ทุกครั้งที่มีการแก้ไขโค้ด, เพิ่มฟีเจอร์, แก้บั๊ก หรือคอมไพล์ตัวติดตั้งใหม่:
- **ต้องปรับเพิ่มเลขเวอร์ชัน (Version Bump) อัตโนมัติทุกครั้งทันที โดยไม่ต้องรอให้ User ทักท้วงหรือบอก**
  - Minor / Major increment (เช่น 1.9 -> 2.0, 2.0 -> 2.1) สำหรับฟีเจอร์ใหม่
  - Patch increment (เช่น 2.0 -> 2.0.1 หรือ 2.1) สำหรับแก้บั๊กย่อย
- จุดที่ต้องอัปเดตเลขเวอร์ชันให้ตรงกันทุกจุด:
  1. `installer\DolphinXVDO_Setup.iss` -> `#define MyAppVersion "X.X"`
  2. `DolphinXVDO\src\Program.cs` -> ข้อความไตเติลหน้าต่าง, Dialog, Tray Icon, Header comment
  3. `DolphinXVDO\package.json` -> `"version": "X.X.0"`
  4. ผลลัพธ์ตัวติดตั้ง Inno Setup จะต้องออกมาเป็นชื่อเวอร์ชันใหม่เสมอ เช่น `DolphinXvdo-Setup-vX.X.exe`

## 2. Audio & DSP Integrity
- รักษาการทำงานของ Equalizer APO, Biquad Filter และ ReaComp VST ให้สมบูรณ์ 100%
- ค่า DSP Benchmark CPU ต้องนิ่งสนิท (~1.2% คงที่) ตามมาตรฐาน ไม่ดึง CPU รวมของ audiodg.exe
- รักษาธีม Cyberpunk Dark ในทุกหน้าต่าง UI
