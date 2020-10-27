# stomrin

## stomrin-web

Run locally:

    FLASK_ENV=development FLASK_APP=stomrin.py STOMRIN_DIR=/tmp/stomrin-state env/bin/flask run

Build and run container:

    buildah bud -t stomrin-web:dev
    podman run --rm -it -e STOMRIN_DIR=/srv -v /tmp/stomrin-state:/srv stomrin-web:dev

## stomrin-worker

Build and run container:

    buildah bud -t stomrin-worker:dev
    podman run --rm -it -e STOMRIN_WATCH_DIR=/srv -v /tmp/stomrin-state:/srv stomrin-worker-dev
