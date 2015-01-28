#!/usr/bin/env python2.7

import setuptools

setuptools.setup(
    name="stomrin-web",
    version="0.0.1",
    author="Joost Molenaar",
    author_email="j.j.molenaar@gmail.com",
    install_requires=["xhttp"],
    py_modules=["stomrin"],
    data_files=[('.', ['./index.html'])])
