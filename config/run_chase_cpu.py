"""Start ML-Agents with CUDA hidden before torch is imported.

The installed torch build cannot run on this machine's RTX 5090. ML-Agents 1.1
initializes torch's default device during import, before it reads YAML settings.
"""

import os

os.environ["CUDA_VISIBLE_DEVICES"] = "-1"

from mlagents.trainers.learn import main


if __name__ == "__main__":
    main()
