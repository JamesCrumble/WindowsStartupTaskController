import sys

from itertools import takewhile, dropwhile

commands_raw = r'''C:\Program Files\Docker\Docker\Docker Desktop.exe -Autostart
"C:\Users\namst\AppData\Local\FluxSoftware\Flux\flux.exe" /noshow
"C:\Program Files\Mem Reduct\memreduct.exe" -minimized
"C:\Users\namst\AppData\Local\Programs\mattermost-desktop\Mattermost.exe"
"C:\Users\namst\AppData\Roaming\uTorrent\uTorrent.exe"  /MINIMIZED
C:\WINDOWS\system32\SecurityHealthSystray.exe
C:\Windows\system32\rundll32.exe C:\Windows\System32\LogiLDA.dll,LogiFetch
"C:\WINDOWS\System32\RtkAudUService64.exe" -background
C:\Program Files (x86)\Cisco\Cisco Secure Client\UI\csc_ui.exe'''

commands_raw_list: list[str] = [
    row.replace('"', '') for row in commands_raw.splitlines()
]


def parse_commands(commands: list[str]) -> list[tuple[str, list[str]]]:
    prased_commands: list[tuple[str, list[str]]] = list()

    for command in commands:
        params = [
            param for param in takewhile(
                lambda command_part: not command_part.endswith('.exe'), command.split(' ')[::-1]  # noqa
            ) if param
        ]

        exec_command: list[str] = list()
        for exec_part in dropwhile(lambda command_part: command_part.endswith('.exe'), command.split('\\')):
            buf = list()
            for part in exec_part.split(' '):
                if part in params:
                    continue
                buf.append(part)
            exec_part = ' '.join(buf)
            if ' ' in exec_part and not any(part.endswith('.exe') for part in exec_part.split(' ')):
                exec_part = f'"{exec_part}"'
            exec_command.append(exec_part)

        exec_command = '\\'.join(exec_command)

        prased_commands.append((exec_command, params))

    return prased_commands


if __name__ == '__main__':
    args = sys.argv
    if not args:
        print('No args provided... Need list of raw commands (not impl)')
        # exit(1)

    commands_raw_list: list[str] = [
        row.replace('"', '') for row in commands_raw.splitlines()
    ]
    for command, params in parse_commands(commands_raw_list):
        print(command, params)
