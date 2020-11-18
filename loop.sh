#!/bin/bash

while [ 1 ]; do
 "$@"
  if [ $? -ne 0 ]; then
    exit 1
  fi
done
