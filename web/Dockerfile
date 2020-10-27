FROM j0057.azurecr.io/alpine:3.12
RUN apk add --no-cache python3 uwsgi uwsgi-http uwsgi-python3 py3-flask
RUN mkdir /app
COPY *.py /app
COPY *.html /app
EXPOSE 8000
WORKDIR /app
CMD ["uwsgi", \
     "--uid", "uwsgi", \
     "--gid", "uwsgi", \
     "--master", \
     "--processes", "4", \
     "--plugin", "python3", \
     "--http-socket", ":8000", \
     "--wsgi-file", "stomrin.py", \
     "--callable", "app"]
