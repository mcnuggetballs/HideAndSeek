1. download python 3.10.12
2. open cmd in project directory, and run either that works:
	python -3.10 -m venv mlagents_env
	py -3.10 -m venv mlagents_env

3. run this, should see (mlagents_env)
mlagents_env\Scripts\activate

python -m pip install --upgrade pip

pip install mlagents==1.1.0 torch==2.1.1

pip install packaging==21.3
pip install setuptools==65.5.0


for installing mlagents
mlagents_env\Scripts\activate
pip install mlagents

to train mlagents:
mlagents_env\Scripts\activate
python -m mlagents.trainers.learn config/config.yaml --run-id=hide_seek_v1


architecture:
game manager
- start/stop game sessions
- select influence map config

ml agent documentation
https://docs.unity3d.com/Packages/com.unity.ml-agents@1.0/api/Unity.MLAgents.Agent.html

ünity background
https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Background-Unity.html
