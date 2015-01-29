
import os
import os.path

import xhttp

STOMRIN_DIR = os.environ.get('STOMRIN_DIR', '.')
print 'STOMRIN_DIR:', STOMRIN_DIR

class StomrinRoot(xhttp.Resource):
    @xhttp.get({ 'postcode?': r'^[0-9]{4}\+[A-Za-z]{2}$',
                 'huisnr?': r'^[1-9][0-9]*$',
                 'toevoeging?': r'^[A-Za-z0-9]*$',
                 'jaar?': '^20[1-9][0-9]$' })
    def GET(self, req):
        if req['x-get']['postcode'] and req['x-get']['huisnr'] and req['x-get']['jaar']:
            req['x-get']['postcode'] = req['x-get']['postcode'].replace(' ', '').lower()
            #print req['x-get']
            basename = '-'.join([ req['x-get']['jaar'], req['x-get']['postcode'], req['x-get']['huisnr'] ])
            basename = os.path.join(STOMRIN_DIR, basename)
            if req['x-get']['toevoeging']: basename += '-' + req['x-get']['toevoeging']
            #print `basename`
            if os.path.exists(basename + '.plz'):
                return { 'x-status': xhttp.status.ACCEPTED }
            if os.path.exists(basename + '.acc'):
                return { 'x-status': xhttp.status.ACCEPTED }
            if os.path.exists(basename + '.err'):
                with open(basename + '.err') as err:
                    return { 'x-status': xhttp.status.NOT_FOUND,
                             'x-content': err.read(),
                             'content-type': 'text/plain' }
            if os.path.exists(basename + '.html'):
                return { 'x-status': xhttp.status.SEE_OTHER,
                         'location': basename + '.html' }
            with open(basename + '.plz', 'w'):
                return { 'x-status': xhttp.status.ACCEPTED }
        else:
            return xhttp.serve_file('index.html', 'application/xhtml+xml')

class StomrinRouter(xhttp.Router):
    def __init__(self):
        super(StomrinRouter, self).__init__(
            ('^/$',                     xhttp.Redirector('/stomrin/')),
            ('^/stomrin/$',             StomrinRoot()),
            ('^/stomrin/(.*\.html)$',   xhttp.FileServer(STOMRIN_DIR, 'application/xhtml+xml'))
        )

class StomrinApp(StomrinRouter):
    @xhttp.xhttp_app
    @xhttp.catcher
    def __call__(self, req, *a, **k):
        #print '->', req
        res = super(type(self), self).__call__(req, *a, **k)
        #print '<-', res
        return res

app = StomrinApp()

if __name__ == '__main__':
    xhttp.run_server(app)
