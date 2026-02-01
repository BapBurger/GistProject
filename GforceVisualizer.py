import pandas as pd
import matplotlib.pyplot as plt
import glob
import os
import sys

# 1. 가장 최근의 CSV 파일 찾기
list_of_files = glob.glob('G_Log_Full_*.csv') 
if not list_of_files:
    print("CSV 파일을 찾을 수 없습니다! 게임을 실행해서 녹화를 먼저 진행해주세요.")
    input("엔터를 누르면 종료합니다...")
    sys.exit()

latest_file = max(list_of_files, key=os.path.getctime)
print(f"Reading file: {latest_file}")

# 2. 데이터 로드
df = pd.read_csv(latest_file)

# 3. 그래프 스타일 설정 (예쁘게!)
plt.style.use('dark_background') # 다크 모드 간지
fig, axes = plt.subplots(3, 1, figsize=(12, 10), sharex=True)
fig.suptitle(f'Motion Cueing Analysis : {latest_file}', fontsize=16, color='white')

# 색상 팔레트
color_input = '#00ff00'   # 라임색 (입력)
color_applied = '#00ffff' # 시안색 (토글 반영)
color_output = '#ff00ff'  # 마젠타 (최종 체감)

# --- (1) Surge Graph ---
ax = axes[0]
ax.plot(df['Time'], df['Input_Surge'], label='Input (Total)', color=color_input, alpha=0.5, linestyle='--')
ax.plot(df['Time'], df['Applied_Surge'], label='Applied (Switch)', color=color_applied, alpha=0.7)
ax.plot(df['Time'], df['Output_Surge'], label='Output (Felt)', color=color_output, linewidth=2)
ax.set_ylabel('Surge (G)')
ax.legend(loc='upper right')
ax.grid(True, alpha=0.3)
ax.set_title("Surge (Longitudinal) - Acceleration/Braking", fontsize=12)

# --- (2) Sway Graph ---
ax = axes[1]
ax.plot(df['Time'], df['Input_Sway'], label='Input', color=color_input, alpha=0.5, linestyle='--')
ax.plot(df['Time'], df['Applied_Sway'], label='Applied', color=color_applied, alpha=0.7)
ax.plot(df['Time'], df['Output_Sway'], label='Output', color=color_output, linewidth=2)
ax.set_ylabel('Sway (G)')
ax.legend(loc='upper right')
ax.grid(True, alpha=0.3)
ax.set_title("Sway (Lateral) - Cornering/Bolster", fontsize=12)

# --- (3) Heave Graph ---
ax = axes[2]
ax.plot(df['Time'], df['Input_Heave'], label='Input', color=color_input, alpha=0.5, linestyle='--')
ax.plot(df['Time'], df['Applied_Heave'], label='Applied', color=color_applied, alpha=0.7)
ax.plot(df['Time'], df['Output_Heave'], label='Output', color=color_output, linewidth=2)
ax.set_ylabel('Heave (G)')
ax.set_xlabel('Time (s)')
ax.legend(loc='upper right')
ax.grid(True, alpha=0.3)
ax.set_title("Heave (Vertical) - Road Texture/Waves", fontsize=12)

plt.tight_layout()
plt.show()