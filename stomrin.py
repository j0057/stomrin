#!/usr/bin/env python3

import os.path
import re

from flask import Flask, request, redirect, send_file

STOMRIN_DIR = os.environ.get('STOMRIN_DIR', '.')
print(f"STOMRIN_DIR: {STOMRIN_DIR}");

app = Flask(__name__)

@app.route('/', methods=['GET'])
def get_root():
    # return 200 OK with HTML/CSS/JS if any query params are missing/superfluous
    if {*request.args.keys()} ^ {'postcode', 'huisnr', 'toevoeging', 'jaar'}:
        return send_file('index.html', mimetype='text/html')

    # return 400 Bad Request if any query params are invalid
    if not re.match(r'[0-9]{4} ?[a-zA-Z]{2}', request.args['postcode']):
        return f"ongeldige postcode: {request.args['postcode']!r}", 400
    if not re.match(r'[1-9][0-9]*', request.args['huisnr']):
        return f"ongeldig huisnummer: {request.args['huisnr']!r}", 400
    if not re.match(r'[a-zA-Z0-9-]*', request.args['toevoeging']):
        return f"ongelidge toevoeging: {request.args['toevoeging']!r}", 400
    if not re.match(r'2[0-9]{3}', request.args['jaar']):
        return f"ongeldig jaar: {request.args['jaar']!r}", 400

    # construct basename of calendar
    basename = f"{request.args['jaar']}-{request.args['postcode'].replace(' ', '').lower()}-{request.args['huisnr']}"
    if request.args['toevoeging']:
        basename += f"-{request.args['toevoeging']}"
    print(f"basename: {basename}")

    # return 202 Accepted if job is submitted/running
    if os.path.exists(f"{STOMRIN_DIR}/{basename}.plz"):
        return 'taak afgeleverd', 202
    if os.path.exists(f"{STOMRIN_DIR}/{basename}.acc"):
        return 'taak wordt verwerkt', 202

    # return 400 Bad Request if job has resulted in an error
    if os.path.exists(f"{STOMRIN_DIR}/{basename}.err"):
        with open(basename + '.err', 'r') as f:
            return f.read(), 400

    # return 303 See Other redirecting to the HTML calendar if job has finished successfully
    if os.path.exists(f"{STOMRIN_DIR}/{basename}.html"):
        return redirect(f"/{basename}.html", 303)

    # return 202 Accepted and submit job if not yet submitted
    with open(f"{STOMRIN_DIR}/{basename}.plz", 'w'):
        return 'taak wordt afgeleverd', 202

@app.route('/<filename>', methods=['GET'])
def get_result(filename):
    # return 200 OK with .html
    if re.match(r'2[0-9]{3}-[0-9]{4}[a-z]{2}-[1-9][0-9]*(-[a-z0-9-]+)?\.html', filename):
        return send_file(f"{STOMRIN_DIR}/{filename}", mimetype='application/xhtml+xml')

    # return 200 OK with .ics
    if re.match(r'2[0-9]{3}-[0-9]{4}[a-z]{2}-[1-9][0-9]*(-[a-z0-9-]+)?\.ics', filename):
        return send_file(f"{STOMRIN_DIR}/{filename}", mimetype='text/calendar')

    # return 404 Not Found otherwise
    return f"not found: {filename!r}", 404
