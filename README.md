## Quick Start (Windows)

1. **Install Python 3.10.12:**
	 Other versions of Python __will not__ work!

2. **Create and activate a virtual environment:**
	 - **Windows (PowerShell):**
		 ```powershell
		 py -3.10 -m venv mlagents_env
		 ```
		 If you see an execution policy error, run one of these in PowerShell first:
		 ```powershell
		 Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
		 ```
	 - **Windows (Command Prompt):**
		 ```cmd
		 py -3.10 -m venv mlagents_env
		 ```

3. **Activate the virtual environment:**
	 - **Windows (PowerShell):**
		 ```powershell
		 .\mlagents_env\Scripts\Activate.ps1
		 ```
	 - **Windows (Command Prompt):**
		 ```cmd
		 mlagents_env\Scripts\activate
		 ```

4. **Install dependencies:**
	 ```bash
	 pip install -r requirements.txt
	 ```

5. **Run the training script:**
	 ```bash
    python -m mlagents.trainers.learn config/config.yaml --run-id=hide_seek_v1
	 ```

## Architecture
game manager
- start/stop game sessions
- select influence map config

## ML Agent Documentation
https://docs.unity3d.com/Packages/com.unity.ml-agents@1.0/api/Unity.MLAgents.Agent.html

## Unity Background
https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Background-Unity.html