import pandas as pd
import matplotlib.pyplot as plt
import glob
import os
import sys

# 1. 최신 CSV 찾기
list_of_files = glob.glob('G_Log_Gain_*.csv') 
if not list_of_files:
    print("CSV 파일을 찾을 수 없습니다! 녹화를 먼저 진행해주세요.")
    input("엔터를 누르면 종료합니다...")
    sys.exit()

latest_file = max(list_of_files, key=os.path.getctime)
print(f"Reading file: {latest_file}")

# 2. 데이터 로드
df = pd.read_csv(latest_file)

# 3. 그래프 그리기
plt.style.use('dark_background')
fig, axes = plt.subplots(3, 1, figsize=(12, 10), sharex=True)
fig.suptitle(f'Motion Cueing Analysis (with Master Gain) : {latest_file}', fontsize=16, color='white')

# 색상 정의
c_raw = '#555555'    # 회색 (Input: 너무 크면 보기 싫으니 흐리게)
c_gain = '#00ffff'   # 청록색 (Gain: ★ 주인공)
c_out = '#ff00ff'    # 마젠타 (Output: 실제 움직임)

# (1) Surge
ax = axes[0]
ax.plot(df['Time'], df['Input_Surge'], label='Raw Input (Total)', color=c_raw, alpha=0.5, linestyle='--')
ax.plot(df['Time'], df['Gain_Surge'], label='Gain Adjusted (Command)', color=c_gain, linewidth=2)
ax.plot(df['Time'], df['Output_Surge'], label='Actual Output (Seat)', color=c_out, linewidth=2, linestyle=':')
ax.set_ylabel('Surge (G)')
ax.legend(loc='upper right')
ax.grid(True, alpha=0.3)
ax.set_title("Surge (Longitudinal)", fontsize=12)

# (2) Sway
ax = axes[1]
ax.plot(df['Time'], df['Input_Sway'], label='Raw Input', color=c_raw, alpha=0.5, linestyle='--')
ax.plot(df['Time'], df['Gain_Sway'], label='Gain Adjusted', color=c_gain, linewidth=2)
ax.plot(df['Time'], df['Output_Sway'], label='Actual Output', color=c_out, linewidth=2, linestyle=':')
ax.set_ylabel('Sway (G)')
ax.legend(loc='upper right')
ax.grid(True, alpha=0.3)
ax.set_title("Sway (Lateral)", fontsize=12)

# (3) Heave
ax = axes[2]
ax.plot(df['Time'], df['Input_Heave'], label='Raw Input', color=c_raw, alpha=0.5, linestyle='--')
ax.plot(df['Time'], df['Gain_Heave'], label='Gain Adjusted', color=c_gain, linewidth=2)
ax.plot(df['Time'], df['Output_Heave'], label='Actual Output', color=c_out, linewidth=2, linestyle=':')
ax.set_ylabel('Heave (G)')
ax.set_xlabel('Time (s)')
ax.legend(loc='upper right')
ax.grid(True, alpha=0.3)
ax.set_title("Heave (Vertical)", fontsize=12)

plt.tight_layout()
plt.show()